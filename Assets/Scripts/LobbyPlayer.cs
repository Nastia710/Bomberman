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

        public override void OnNetworkSpawn()
        {
            _nickname.OnValueChanged += OnNicknameChanged;
            UpdateNicknameDisplay(_nickname.Value.ToString());

            if (IsOwner)
            {
                string localNickname = LobbyManager.PlayerName;
                _nickname.Value = localNickname;
            }
        }

        public override void OnNetworkDespawn()
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
