using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Bomberman
{
    public class PathFinder : MonoBehaviour
    {
        [SerializeField]
        private LayerMask _solidLayer;

        private List<Vector2> _pathToTarget;
        private List<Node> _checkedNodes = new List<Node>();
        private List<Node> _waitingNodes = new List<Node>();

        public List<Node> FreeNodes = new List<Node>();

        public List<Vector2> GetPath(Vector2 target)
        {
            _pathToTarget = new List<Vector2>();
            _checkedNodes = new List<Node>();
            _waitingNodes = new List<Node>();

            Vector2 startPosition = new Vector2(
                Mathf.Round(transform.position.x),
                Mathf.Round(transform.position.y));
            Vector2 targetPosition = new Vector2(
                Mathf.Round(target.x),
                Mathf.Round(target.y));

            if (startPosition == targetPosition)
            {
                return _pathToTarget;
            }

            Node startNode = new Node(0, startPosition, targetPosition, null);
            _checkedNodes.Add(startNode);
            _waitingNodes.AddRange(GetNeighbourNodes(startNode));

            while (_waitingNodes.Count > 0)
            {
                Node nodeToCheck = _waitingNodes
                    .Where(x => x.F == _waitingNodes.Min(y => y.F))
                    .FirstOrDefault();

                if (nodeToCheck.Position == targetPosition)
                {
                    return CalculatePathFromNode(nodeToCheck);
                }

                bool isWalkable = !Physics2D.OverlapCircle(nodeToCheck.Position, 0.1f, _solidLayer);

                if (!isWalkable)
                {
                    _waitingNodes.Remove(nodeToCheck);
                    _checkedNodes.Add(nodeToCheck);
                }
                else
                {
                    _waitingNodes.Remove(nodeToCheck);

                    if (!_checkedNodes.Where(x => x.Position == nodeToCheck.Position).Any())
                    {
                        _checkedNodes.Add(nodeToCheck);
                        _waitingNodes.AddRange(GetNeighbourNodes(nodeToCheck));
                    }
                }
            }

            FreeNodes = _checkedNodes;
            return _pathToTarget;
        }

        private List<Vector2> CalculatePathFromNode(Node node)
        {
            List<Vector2> path = new List<Vector2>();
            Node currentNode = node;

            while (currentNode.PreviousNode != null)
            {
                path.Add(new Vector2(currentNode.Position.x, currentNode.Position.y));
                currentNode = currentNode.PreviousNode;
            }

            return path;
        }

        private List<Node> GetNeighbourNodes(Node node)
        {
            List<Node> neighbours = new List<Node>();

            neighbours.Add(new Node(node.G + 1,
                new Vector2(node.Position.x - 1, node.Position.y),
                node.TargetPosition, node));
            neighbours.Add(new Node(node.G + 1,
                new Vector2(node.Position.x + 1, node.Position.y),
                node.TargetPosition, node));
            neighbours.Add(new Node(node.G + 1,
                new Vector2(node.Position.x, node.Position.y - 1),
                node.TargetPosition, node));
            neighbours.Add(new Node(node.G + 1,
                new Vector2(node.Position.x, node.Position.y + 1),
                node.TargetPosition, node));

            return neighbours;
        }

        private void OnDrawGizmos()
        {
            foreach (Node item in _checkedNodes)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(new Vector2(item.Position.x, item.Position.y), 0.1f);
            }

            if (_pathToTarget != null)
            {
                foreach (Vector2 item in _pathToTarget)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(new Vector2(item.x, item.y), 0.2f);
                }
            }
        }
    }
}