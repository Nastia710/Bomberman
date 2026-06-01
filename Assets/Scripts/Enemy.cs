using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Bomberman
{
	public class Enemy : NetworkBehaviour
	{
		public static List<Enemy> AllEnemies = new List<Enemy>();

		[SerializeField]
		private GameObject _deathEffect;
		[SerializeField]
		private float _moveSpeed;

		private List<Vector2> _pathToBomberMan = new List<Vector2>();
		private List<Vector2> _randomPath = new List<Vector2>();
		private List<Vector2> _currentPath = new List<Vector2>();
		private PathFinder _pathFinder;
		private bool _isMoving;

		private void Awake()
		{
			_pathFinder = GetComponent<PathFinder>();
		}

		private void OnEnable()
		{
			SubscribeEvents();
		}

		private void Start()
		{
			if (!IsServer)
			{
				return;
			}
			ReCalculatePath();
			_isMoving = true;
		}

		private void Update()
		{
			if (!IsServer || !IsSpawned)
			{
				return;
			}

			if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
			{
				return;
			}

			BomberMan closestPlayer = FindClosestPlayer();
			if (closestPlayer == null)
			{
				return;
			}

			if (_currentPath.Count == 0 && Vector2.Distance(transform.position, closestPlayer.transform.position) > 0.5f)
			{
				ReCalculatePath();
				_isMoving = true;
			}

			if (_currentPath.Count == 0)
			{
				return;
			}

			if (_isMoving)
			{
				Vector2 targetCell = _currentPath[_currentPath.Count - 1];

				if (Vector2.Distance(transform.position, targetCell) > 0.1f)
				{
					transform.position = Vector2.MoveTowards(
						transform.position,
						targetCell,
						_moveSpeed * Time.deltaTime);
				}

				if (Vector2.Distance(transform.position, targetCell) <= 0.1f)
				{
					_isMoving = false;
				}
			}
			else
			{
				ReCalculatePath();
				_isMoving = true;
			}
		}

		private void OnDisable()
		{
			UnsubscribeEvents();
		}

		public override void OnNetworkSpawn()
		{
			if (IsServer)
			{
				if (!AllEnemies.Contains(this))
				{
					AllEnemies.Add(this);
				}
			}
			base.OnNetworkSpawn();
		}

		public override void OnNetworkDespawn()
		{
			if (IsServer)
			{
				AllEnemies.Remove(this);
			}
			gameObject.SetActive(false);
			base.OnNetworkDespawn();
		}

		public void ReCalculatePath()
		{
			BomberMan closestPlayer = FindClosestPlayer();
			if (closestPlayer != null)
			{
				_pathToBomberMan = _pathFinder.GetPath(closestPlayer.transform.position);
			}
			else
			{
				_pathToBomberMan.Clear();
			}

			if (_pathToBomberMan.Count == 0)
			{
				if (_pathFinder.FreeNodes.Count > 0)
				{
					int randomIndex = Random.Range(0, _pathFinder.FreeNodes.Count);
					_randomPath = _pathFinder.GetPath(_pathFinder.FreeNodes[randomIndex].Position);
					_currentPath = _randomPath;
				}
			}
			else
			{
				_currentPath = _pathToBomberMan;
			}
		}

		public void Damage(int source)
		{
			if (!IsServer)
			{
				return;
			}

			NetworkObject netObj = GetComponent<NetworkObject>();
			if (netObj == null || !netObj.IsSpawned)
			{
				return;
			}

			if (source == Bomberman.Damage.FIRE_DAMAGE)
			{
				SpawnDeathEffectClientRpc(transform.position);
				netObj.Despawn(false);
			}
		}

		private BomberMan FindClosestPlayer()
		{
			BomberMan closest = null;
			float minDistance = float.MaxValue;

			foreach (var player in BomberMan.AllPlayers)
			{
				if (player != null && player.GetComponent<NetworkObject>().IsSpawned && !player.IsDead)
				{
					float distance = Vector2.Distance(transform.position, player.transform.position);
					if (distance < minDistance)
					{
						minDistance = distance;
						closest = player;
					}
				}
			}

			return closest;
		}

		private void SubscribeEvents()
		{
			if (!IsServer)
			{
				return;
			}
			BomberMan.BombPlacedEvent += ReCalculatePath;
		}

		private void UnsubscribeEvents()
		{
			if (!IsServer)
			{
				return;
			}
			BomberMan.BombPlacedEvent -= ReCalculatePath;
		}

		[ClientRpc]
		private void SpawnDeathEffectClientRpc(Vector3 position)
		{
			Instantiate(_deathEffect, position, Quaternion.identity);
		}
	}
}
