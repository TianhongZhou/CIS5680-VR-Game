using System.Collections.Generic;
using UnityEngine;

namespace CIS5680VRGame.Generation
{
    [DisallowMultipleComponent]
    public class MazeGridNavigator : MonoBehaviour
    {
        readonly struct OpenNode
        {
            public OpenNode(Vector2Int position, int priority, int heuristic)
            {
                Position = position;
                Priority = priority;
                Heuristic = heuristic;
            }

            public Vector2Int Position { get; }
            public int Priority { get; }
            public int Heuristic { get; }
        }

        sealed class OpenNodeMinHeap
        {
            readonly List<OpenNode> m_Items = new();

            public int Count => m_Items.Count;

            public void Clear()
            {
                m_Items.Clear();
            }

            public void Enqueue(OpenNode node)
            {
                m_Items.Add(node);
                SiftUp(m_Items.Count - 1);
            }

            public OpenNode Dequeue()
            {
                OpenNode root = m_Items[0];
                int lastIndex = m_Items.Count - 1;
                m_Items[0] = m_Items[lastIndex];
                m_Items.RemoveAt(lastIndex);

                if (m_Items.Count > 0)
                    SiftDown(0);

                return root;
            }

            void SiftUp(int index)
            {
                while (index > 0)
                {
                    int parentIndex = (index - 1) / 2;
                    if (!IsHigherPriority(index, parentIndex))
                        break;

                    Swap(index, parentIndex);
                    index = parentIndex;
                }
            }

            void SiftDown(int index)
            {
                while (true)
                {
                    int leftChild = index * 2 + 1;
                    int rightChild = leftChild + 1;
                    int bestIndex = index;

                    if (leftChild < m_Items.Count && IsHigherPriority(leftChild, bestIndex))
                        bestIndex = leftChild;

                    if (rightChild < m_Items.Count && IsHigherPriority(rightChild, bestIndex))
                        bestIndex = rightChild;

                    if (bestIndex == index)
                        return;

                    Swap(index, bestIndex);
                    index = bestIndex;
                }
            }

            bool IsHigherPriority(int leftIndex, int rightIndex)
            {
                OpenNode left = m_Items[leftIndex];
                OpenNode right = m_Items[rightIndex];
                if (left.Priority != right.Priority)
                    return left.Priority < right.Priority;

                return left.Heuristic < right.Heuristic;
            }

            void Swap(int a, int b)
            {
                (m_Items[a], m_Items[b]) = (m_Items[b], m_Items[a]);
            }
        }

        [SerializeField] MazeRunBootstrap m_Bootstrap;
        [SerializeField] MazeModulePlacer m_ModulePlacer;

        readonly OpenNodeMinHeap m_OpenNodes = new();
        readonly Dictionary<Vector2Int, Vector2Int> m_CameFrom = new();
        readonly Dictionary<Vector2Int, int> m_GScore = new();
        readonly HashSet<Vector2Int> m_ClosedSet = new();
        readonly List<Vector2Int> m_ScratchNeighbors = new(4);

        public MazeRunBootstrap Bootstrap => m_Bootstrap;
        public MazeModulePlacer ModulePlacer => m_ModulePlacer;
        public MazeLayout CurrentLayout => m_Bootstrap != null ? m_Bootstrap.CurrentLayout : null;
        public bool HasActiveLayout => CurrentLayout != null && m_ModulePlacer != null;
        public float CellSize => m_ModulePlacer != null ? m_ModulePlacer.CellSize : 0f;
        public Vector3 GridOrigin => m_ModulePlacer != null ? m_ModulePlacer.ResolvedPlacementOrigin : Vector3.zero;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        public bool TryGetCurrentLayout(out MazeLayout layout)
        {
            layout = CurrentLayout;
            return layout != null;
        }

        public Vector3 GetCellWorldCenter(Vector2Int gridPosition)
        {
            return m_ModulePlacer != null
                ? m_ModulePlacer.GetCellWorldPosition(gridPosition)
                : Vector3.zero;
        }

        public bool TryGetGridPosition(Vector3 worldPosition, out Vector2Int gridPosition)
        {
            if (m_ModulePlacer == null)
            {
                gridPosition = default;
                return false;
            }

            gridPosition = m_ModulePlacer.WorldToGridPosition(worldPosition);
            return true;
        }

        public bool TryGetCell(Vector2Int gridPosition, out MazeCellData cell)
        {
            MazeLayout layout = CurrentLayout;
            if (layout == null)
            {
                cell = null;
                return false;
            }

            return layout.TryGetCell(gridPosition, out cell);
        }

        public bool TryGetCellAtWorldPosition(Vector3 worldPosition, out MazeCellData cell)
        {
            cell = null;
            if (!TryGetGridPosition(worldPosition, out Vector2Int gridPosition))
                return false;

            if (TryGetCell(gridPosition, out cell))
                return true;

            return TryGetNearestCell(worldPosition, out cell);
        }

        public bool TryGetNearestCell(Vector3 worldPosition, out MazeCellData cell)
        {
            MazeLayout layout = CurrentLayout;
            if (layout == null || layout.Cells.Count == 0)
            {
                cell = null;
                return false;
            }

            cell = null;
            float bestDistanceSqr = float.PositiveInfinity;
            Vector3 planarWorldPosition = new(worldPosition.x, 0f, worldPosition.z);

            for (int i = 0; i < layout.Cells.Count; i++)
            {
                MazeCellData candidate = layout.Cells[i];
                Vector3 candidateCenter = GetCellWorldCenter(candidate.GridPosition);
                Vector3 planarCandidateCenter = new(candidateCenter.x, 0f, candidateCenter.z);
                float distanceSqr = (planarCandidateCenter - planarWorldPosition).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                cell = candidate;
            }

            return cell != null;
        }

        public int GetNeighbors(Vector2Int gridPosition, List<Vector2Int> results)
        {
            if (results == null)
                return 0;

            results.Clear();
            if (!TryGetCell(gridPosition, out MazeCellData cell))
                return 0;

            AppendNeighborIfConnected(cell, MazeCellConnection.North, Vector2Int.up, results);
            AppendNeighborIfConnected(cell, MazeCellConnection.East, Vector2Int.right, results);
            AppendNeighborIfConnected(cell, MazeCellConnection.South, Vector2Int.down, results);
            AppendNeighborIfConnected(cell, MazeCellConnection.West, Vector2Int.left, results);
            return results.Count;
        }

        public bool TryFindPath(Vector2Int startGridPosition, Vector2Int targetGridPosition, List<Vector2Int> pathBuffer)
        {
            if (pathBuffer == null)
                return false;

            pathBuffer.Clear();
            if (!TryGetCell(startGridPosition, out _) || !TryGetCell(targetGridPosition, out _))
                return false;

            if (startGridPosition == targetGridPosition)
            {
                pathBuffer.Add(startGridPosition);
                return true;
            }

            m_OpenNodes.Clear();
            m_CameFrom.Clear();
            m_GScore.Clear();
            m_ClosedSet.Clear();

            m_CameFrom[startGridPosition] = startGridPosition;
            m_GScore[startGridPosition] = 0;
            m_OpenNodes.Enqueue(new OpenNode(
                startGridPosition,
                EstimateRemainingCost(startGridPosition, targetGridPosition),
                EstimateRemainingCost(startGridPosition, targetGridPosition)));

            while (m_OpenNodes.Count > 0)
            {
                OpenNode openNode = m_OpenNodes.Dequeue();
                Vector2Int current = openNode.Position;
                if (m_ClosedSet.Contains(current))
                    continue;

                if (!m_GScore.TryGetValue(current, out int currentGScore))
                    continue;

                int expectedPriority = currentGScore + EstimateRemainingCost(current, targetGridPosition);
                if (openNode.Priority > expectedPriority)
                    continue;

                if (current == targetGridPosition)
                    break;

                m_ClosedSet.Add(current);

                GetNeighbors(current, m_ScratchNeighbors);
                for (int i = 0; i < m_ScratchNeighbors.Count; i++)
                {
                    Vector2Int neighbor = m_ScratchNeighbors[i];
                    if (m_ClosedSet.Contains(neighbor))
                        continue;

                    int tentativeGScore = currentGScore + 1;
                    if (m_GScore.TryGetValue(neighbor, out int existingGScore) && tentativeGScore >= existingGScore)
                        continue;

                    int heuristic = EstimateRemainingCost(neighbor, targetGridPosition);
                    m_CameFrom[neighbor] = current;
                    m_GScore[neighbor] = tentativeGScore;
                    m_OpenNodes.Enqueue(new OpenNode(neighbor, tentativeGScore + heuristic, heuristic));
                }
            }

            if (!m_CameFrom.ContainsKey(targetGridPosition))
                return false;

            Vector2Int step = targetGridPosition;
            pathBuffer.Add(step);
            while (step != startGridPosition)
            {
                step = m_CameFrom[step];
                pathBuffer.Add(step);
            }

            pathBuffer.Reverse();
            return true;
        }

        public bool TryFindPath(Vector3 startWorldPosition, Vector3 targetWorldPosition, List<Vector2Int> pathBuffer)
        {
            if (!TryGetCellAtWorldPosition(startWorldPosition, out MazeCellData startCell)
                || !TryGetCellAtWorldPosition(targetWorldPosition, out MazeCellData targetCell))
            {
                return false;
            }

            return TryFindPath(startCell.GridPosition, targetCell.GridPosition, pathBuffer);
        }

        static int EstimateRemainingCost(Vector2Int from, Vector2Int to)
        {
            return Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);
        }

        void ResolveReferences()
        {
            if (m_Bootstrap == null)
                m_Bootstrap = GetComponent<MazeRunBootstrap>();

            if (m_Bootstrap == null)
                m_Bootstrap = FindObjectOfType<MazeRunBootstrap>();

            if (m_ModulePlacer == null && m_Bootstrap != null)
                m_ModulePlacer = m_Bootstrap.ModulePlacer;

            if (m_ModulePlacer == null)
                m_ModulePlacer = GetComponent<MazeModulePlacer>();

            if (m_ModulePlacer == null)
                m_ModulePlacer = FindObjectOfType<MazeModulePlacer>();
        }

        void AppendNeighborIfConnected(
            MazeCellData cell,
            MazeCellConnection requiredConnection,
            Vector2Int direction,
            List<Vector2Int> results)
        {
            if (cell == null || results == null)
                return;

            if (m_Bootstrap != null)
            {
                if (!m_Bootstrap.IsMazeConnectionTraversable(cell, requiredConnection))
                    return;
            }
            else if (!cell.Connections.HasFlag(requiredConnection))
            {
                return;
            }

            results.Add(cell.GridPosition + direction);
        }
    }
}
