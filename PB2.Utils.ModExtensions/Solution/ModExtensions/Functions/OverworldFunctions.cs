using PhantomBrigade.Functions;
using UnityEngine;

namespace ModExtensions.Functions
{
    public class OverworldFunctions
    {
        public class MyCustomFunction : IOverworldFunction
        {
            public string input;

            public void Run ()
            {
                Debug.Log ($"Running custom function from ModExtensions: {input}");
            }
        }
    }
}