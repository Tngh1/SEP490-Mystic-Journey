using UnityEngine;

namespace EnemyPatrol.Utilites
{
    // Initializes a new default instance of the Utilites class.
    public static class Utilites
    {
        // Executes get random dir operation.
        public static Vector3 GetRandomDir()
        {
            // Randomize the eligible candidates before selecting this gameplay result.
            return new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
        }
    }
}
