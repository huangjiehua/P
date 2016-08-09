﻿
using System;
using System.Collections.Generic;
using System.Linq;

namespace P.PRuntime
{
    public abstract class Fun<T>
    {
        public abstract string Name
        {
            get;
        }

        public abstract void PushFrame(T parent, params PrtValue[] args);

        public abstract void Execute(StateImpl application, T parent);
    }

    public class Event
    {
        public static Event NullEvent;
        public static Event HaltEvent;
        public string name;
        public PrtType payload;
        public int maxInstances;
        public bool doAssume;

        public Event(string name, PrtType payload, int mInstances, bool doAssume)
        {
            this.name = name;
            this.payload = payload;
            this.maxInstances = mInstances;
            this.doAssume = doAssume;
        }
    };

    public class Transition<T>
    {
        public Fun<T> fun; // isPush <==> fun == null
        public State<T> to;

        public Transition(Fun<T> fun, State<T> to)
        {
            this.fun = fun;
            this.to = to;
        }
    };

    public enum StateTemperature
    {
        Cold,
        Warm,
        Hot
    };

    public class State<T>
    {
        public string name;
        public Fun<T> entryFun;
        public Fun<T> exitFun;
        public Dictionary<Event, Transition<T>> transitions;
        public Dictionary<Event, Fun<T>> dos;
        public bool hasNullTransition;
        public StateTemperature temperature;
        public HashSet<Event> deferredSet;

        public State(string name, Fun<T> entryFun, Fun<T> exitFun, bool hasNullTransition, StateTemperature temperature)
        {
            this.name = name;
            this.entryFun = entryFun;
            this.exitFun = exitFun;
            this.transitions = new Dictionary<Event, Transition<T>>();
            this.dos = new Dictionary<Event, Fun<T>>();
            this.hasNullTransition = hasNullTransition;
            this.temperature = temperature;
        }

        public Transition<T> FindPushTransition(Event evt)
        {
            if (transitions.ContainsKey(evt))
            {
                Transition<T> transition = transitions[evt];
                if (transition.fun == null)
                    return transition;
            }
            return null;
        }

        public Transition<T> FindTransition(Event evt)
        {
            if (transitions.ContainsKey(evt))
            {
                return transitions[evt];
            }
            else
            {
                return null;
            }
        }
    };

    public class EventNode
    {
        public Event ev;
        public PrtValue arg;

        public EventNode(Event e, PrtValue payload)
        {
            ev = e;
            arg = payload;
        }
    }

    public class EventBuffer<T> where T: Machine<T>
    {
        List<EventNode> events;
        public EventBuffer()
        {
            events = new List<EventNode>();
        }

        public int Size()
        {
            return events.Count();
        }
        public int CalculateInstances(Event e)
        {
            return events.Select(en => en.ev).Where(ev => ev == e).Count();
        }

        public void EnqueueEvent(Event e, PrtValue arg)
        {
            //TODO : -1 seems odd fix this
            if (e.maxInstances == -1)
            {
                events.Add(new EventNode(e, arg));
            }
            else
            {
                if (CalculateInstances(e) == e.maxInstances)
                {
                    if (e.doAssume)
                    {
                        throw new PrtAssumeFailureException();
                    }
                    else
                    {
                        throw new PrtMaxEventInstancesExceededException(
                            String.Format(@"< Exception > Attempting to enqueue event {0} more than max instance of {1}\n", e.name, e.maxInstances));
                    }
                }
                else
                {
                    events.Add(new EventNode(e, arg));
                }
            }
        }

        public void DequeueEvent(T owner)
        {
            HashSet<Event> deferredSet;
            HashSet<Event> receiveSet;

            deferredSet = owner.stateStack.deferredSet;
            receiveSet = owner.receiveSet;

            int iter = 0;
            while (iter < events.Count)
            { 
                if ((receiveSet.Count == 0 && !deferredSet.Contains(events[iter].ev))
                    || (receiveSet.Count > 0 && receiveSet.Contains(events[iter].ev)))
                {
                    owner.currentEvent = events[iter].ev;
                    owner.currentArg = events[iter].arg;
                    events.Remove(events[iter]);
                    return;
                }
                else
                {
                    continue;
                }
            }



        }

        public bool IsEnabled(T owner)
        {
            HashSet<Event> deferredSet;
            HashSet<Event> receiveSet;

            deferredSet = owner.stateStack.deferredSet;
            receiveSet = owner.receiveSet;
            foreach (var evNode in events)
            {
                if ((receiveSet.Count == 0 && !deferredSet.Contains(evNode.ev))
                    || (receiveSet.Count > 0 && receiveSet.Contains(evNode.ev)))
                {
                    return true;
                }

            }
            return false;
        }
    }

    public class StateStack<T>
    {
        public State<T> state;
        public HashSet<Event> deferredSet;
        public HashSet<Event> actionSet;
        public StateStack<T> next;

        public Fun<T> Find(Event f)
        {
            if (state.dos.ContainsKey(f))
            {
                return state.dos[f];
            }
            else
            {
                return next.Find(f);
            }
        }

        public void CalculateDeferredAndActionSet()
        {
            deferredSet = new HashSet<Event>();
            if (next != null)
            {
                deferredSet.UnionWith(next.deferredSet);
            }
            deferredSet.UnionWith(state.deferredSet);
            deferredSet.ExceptWith(state.dos.Keys);
            deferredSet.ExceptWith(state.transitions.Keys);

            actionSet = new HashSet<Event>();
            if (next != null)
            {
                actionSet.UnionWith(next.actionSet);
            }
            actionSet.ExceptWith(state.deferredSet);
            actionSet.UnionWith(state.dos.Keys);
            actionSet.ExceptWith(state.transitions.Keys);
        }

        public bool HasNullTransitionOrAction()
        {
            if (state.hasNullTransition) return true;
            return actionSet.Contains(Event.NullEvent);
        }
    }

    public enum ContinuationReason : int
    {
        Return,
        Nondet,
        Pop,
        Raise,
        Receive,
        Send,
        NewMachine,
    };

    public class ContStackFrame
    {
        public int returnTo;
        public List<PrtValue> locals;
        public ContStackFrame(int ret, List<PrtValue> loc)
        {
            returnTo = ret;
            locals = loc.ToList();
        }
    }

    public class Continuation
    {
        public Stack<ContStackFrame> contStack;
        public ContinuationReason reason;
        public PrtMachine createdMachine;
        public PrtValue retVal;
        public List<PrtValue> retLocals;

        // The nondet field is different from the fields above because it is used 
        // by ReentrancyHelper to pass the choice to the nondet choice point.
        // Therefore, nondet should not be reinitialized in this class.
        public bool nondet;

        public ContStackFrame TopOfStack
        {
            get
            {
                return contStack.Peek();
            }
        }

        public Continuation()
        {
            contStack = new Stack<ContStackFrame>();
            reason = ContinuationReason.Return;
            createdMachine = null;
            retVal = null;
            nondet = false;
            retLocals = null;
        }

        public void Reset()
        {
            contStack = new Stack<ContStackFrame>();
            createdMachine = null;
            retVal = null;
            nondet = false;
            retLocals = null;
        }

        public ContStackFrame PopContFrame()
        {
            return contStack.Pop();
        }

        public void PushContFrame(int ret, List<PrtValue> locals)
        {
            contStack.Push(new ContStackFrame(ret, locals));
        }

        public void Return(List<PrtValue> retLocals)
        {
            Reset();
            reason = ContinuationReason.Return;
            this.retVal = PrtValue.NullValue;
            this.retLocals = retLocals;
        }

        public void ReturnVal(PrtValue val, List<PrtValue> retLocals)
        {

            Reset();
            reason = ContinuationReason.Return;
            this.retVal = val;
            this.retLocals = retLocals;
        }

        public void Pop()
        {
            Reset();
            this.reason = ContinuationReason.Pop;
        }

        public void Raise()
        {
            Reset();
            this.reason = ContinuationReason.Raise;
        }

        public void Send(int ret, List<PrtValue> locals)
        {
            Reset();
            this.reason = ContinuationReason.Send;
            this.PushContFrame(ret, locals);
        }

        void NewMachine(int ret, List<PrtValue> locals, PrtMachine o)
        {
            Reset();
            this.reason = ContinuationReason.NewMachine;
            this.createdMachine = o;
            this.PushContFrame(ret, locals);
        }

        void Receive(int ret, List<PrtValue> locals)
        {
            Reset();
            this.reason = ContinuationReason.Receive;
            this.PushContFrame(ret, locals);
        }

        void Nondet(int ret, List<PrtValue> locals)
        {
            Reset();
            this.reason = ContinuationReason.Nondet;
            this.PushContFrame(ret, locals);
        }
    }
}