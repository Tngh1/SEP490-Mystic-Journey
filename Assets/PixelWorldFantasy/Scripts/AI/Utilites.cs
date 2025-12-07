using UnityEngine;

namespace EnemyPatrol.Utilites
{
    public static class Utilites
    {
        public static Vector3 GetRandomDir()
        {
            return new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
        }
    }
}
