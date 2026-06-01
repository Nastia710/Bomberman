using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

namespace Bomberman
{
	public class LobbyPlayer : NetworkBehaviour
	{
		[SerializeField]
		private TMP_Text _nicknameText;

		private NetworkVariable<FixedString64Bytes> _nickname = new NetworkVariable<FixedString64Bytes>(
			"",
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Owner);

		private void OnEnable()
		{
			_nickname.OnValueChanged += OnNicknameChanged;
		}

		public override void OnNetworkSpawn()
		{
			UpdateNicknameDisplay(_nickname.Value.ToString());

			if (IsOwner)
			{
				string localNickname = LobbyManager.PlayerName;
				_nickname.Value = localNickname;
			}
		}

		private void OnDisable()
		{
			_nickname.OnValueChanged -= OnNicknameChanged;
		}

		private void OnNicknameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
		{
			UpdateNicknameDisplay(newValue.ToString());
		}

		private void UpdateNicknameDisplay(string nickname)
		{
			if (_nicknameText != null)
			{
				_nicknameText.text = nickname;
			}
		}
	}
}
