using UnityEngine;

namespace Bomberman
{
    public class Node
    {
        public Vector2 Position;
        public Vector2 TargetPosition;
        public Node PreviousNode;

        public int F;
        public int G;
        public int H;

        public Node(int g, Vector2 nodePosition, Vector2 targetPosition, Node previousNode)
        {
            Position = nodePosition;
            TargetPosition = targetPosition;
            PreviousNode = previousNode;
            G = g;
            H = (int)Mathf.Abs(targetPosition.x - Position.x)
                + (int)Mathf.Abs(targetPosition.y - Position.y);
            F = G + H;
        }
    }
}
