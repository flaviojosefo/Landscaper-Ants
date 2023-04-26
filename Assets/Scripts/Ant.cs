using UnityEngine;

namespace LandscaperAnts
{
    public sealed class Ant
    {
        public Food Food { get; set; }

        public Vector2Int ColonyCell { get; private set; }

        public Vector2Int CurrentCell { get; set; }

        // Creates an Ant on the colony's position
        public Ant(Vector2Int colony)
        {
            ColonyCell = CurrentCell = colony;
        }

        // Creates an Ant on a given "starter" position
        public Ant(Vector2Int colony, Vector2Int start)
        {
            ColonyCell = colony;
            CurrentCell = start;
        }

        // Check if the Ant is (back) at home
        public bool IsHome() => CurrentCell == ColonyCell;

        public bool HasFood() => Food is not null;

        // Function -> https://www.desmos.com/calculator/uszs8torq5
        public float DropPheromone(float deposit, float minPercentage = 0f)
        {
            float totalDistance = Vector2.Distance(Food.Cell, ColonyCell);

            // Prevent division by 0
            if (Mathf.Approximately(totalDistance, 0f))
            {
                return deposit;
            }

            float currentDistanceToHome = Vector2.Distance(CurrentCell, ColonyCell);

            float minDepositPercentage = deposit * minPercentage;

            return ((currentDistanceToHome * (deposit - minDepositPercentage)) / totalDistance) + minDepositPercentage;
        }
    }
}
