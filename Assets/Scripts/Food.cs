using UnityEngine;

namespace LandscaperAnts {

    public sealed class Food {

        public int Bites { get; private set; }

        public Vector2Int Cell { get; }

        public Food(Vector2Int cell, int maxBites) {

            Cell = cell;
            Bites = maxBites;
        }

        public bool HasBitesLeft() => Bites > 0;

        // Behaviour of biting is in a function because we
        // might later want to implement 'biting' as a percentage
        public void TakeABite() => Bites--;
    }
}
