using UnityEngine;

namespace Bomberman
{
    public class Node
    {
        public Vector2 Position;
        public Vector2 TargetPosition;
        public Node PreviousNode;

		public int TotalCost;
		public int MovementCost;
		public int HeuristicCost;

		public Node(int movementCost, Vector2 nodePosition, Vector2 targetPosition, Node previousNode)
		{
			Position = nodePosition;
			TargetPosition = targetPosition;
			PreviousNode = previousNode;
			MovementCost = movementCost;
			HeuristicCost = (int)Mathf.Abs(targetPosition.x - Position.x)
				+ (int)Mathf.Abs(targetPosition.y - Position.y);
			TotalCost = MovementCost + HeuristicCost;
		}
    }
}
