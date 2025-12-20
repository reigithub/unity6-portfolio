using System;
using UnityEngine;

namespace Game.Core
{
    public abstract class GameService
    {
        protected internal virtual void Startup()
        {
        }

        protected internal virtual void Shutdown()
        {
        }

        protected internal virtual bool AllowResidentOnMemory => false;
    }

    public class HelloWorldGameService : GameService
    {
        protected internal override void Startup()
        {
        }

        protected internal override void Shutdown()
        {
        }

        protected internal override bool AllowResidentOnMemory => true;

        public void HelloWorld()
        {
            Debug.Log("Hello World!!");
        }
    }
}