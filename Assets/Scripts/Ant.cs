using UnityEngine;

namespace LandscaperAnts {

    public sealed class Ant {

        public bool HasFood { get; set; }

        public Vector2Int ColonyCell { get; private set; }

        public Vector2Int CurrentCell { get; set; }

        // Creates an Ant on the colony's position
        public Ant(Vector2Int colony) {

            ColonyCell = CurrentCell = colony;
        }

        // Creates an Ant on a given "starter" position
        public Ant(Vector2Int colony, Vector2Int start) {

            ColonyCell = colony;
            CurrentCell = start;
        }

        // Check if the Ant is (back) at home
        public bool IsHome() => CurrentCell == ColonyCell;
    }
}
