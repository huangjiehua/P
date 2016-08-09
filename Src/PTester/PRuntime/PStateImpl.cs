﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace P.PRuntime
{
    public abstract class StateImpl
    {
        #region Constructors
        /// <summary>
        /// This function is called when the stateimp is loaded first time.
        /// </summary>
        protected StateImpl()
        {
            statemachines = new Dictionary<int, PrtMachine>();
            nextStateMachineId = 0;
        }
        #endregion

        public void AddStateMachine(PrtMachine machine)
        {
            statemachines.Add(nextStateMachineId, machine);
            nextStateMachineId++;
        }

        /// <summary>
        /// Map from the statemachine id to the instance of the statemachine.
        /// </summary>
        private Dictionary<int, PrtMachine> statemachines;

        /// <summary>
        /// Represents the next statemachine id.  
        /// </summary>
        private int nextStateMachineId;

        public abstract IEnumerable<PrtMachine> AllAliveMachines
        {
            get;
        }

        public abstract IEnumerable<PrtMonitor> AllInstalledMonitors
        {
            get;
        }

        public bool Deadlock
        {
            get
            {
                bool enabled = false;
                foreach (var x in AllAliveMachines)
                {
                    if (enabled) break;
                    enabled = enabled || x.IsEnabled;
                }
                bool hot = false;
                foreach (var x in AllInstalledMonitors)
                {
                    if (hot) break;
                    hot = hot || x.IsHot;
                }
                return (!enabled && hot);
            }
        }

        public void Trace(string message, params object[] arguments)
        {
            Console.WriteLine(String.Format(message, arguments));
        }

        public Exception Exception
        {
            get { return exception; }
            set { exception = value; }
        }

        private Exception exception;

        private bool isCall;

        //IExplorable
        public bool IsCall
        {
            get { return isCall; }
            set { isCall = value; }
        }

        private bool isReturn;

        //IExplorable
        public bool IsReturn
        {
            get { return isReturn; }
            set { isReturn = value; }
        }

        public void SetPendingChoicesAsBoolean(PrtMachine process)
        {
            throw new NotImplementedException();
        }

        public object GetSelectedChoiceValue(PrtMachine process)
        {
            throw new NotImplementedException();
        }
    }

    
}
