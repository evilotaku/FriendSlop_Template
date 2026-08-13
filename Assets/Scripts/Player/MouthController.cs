using Unity.Services.Vivox;
using UnityEngine;

public class MouthController : MonoBehaviour
{
    [SerializeField] private PlayerData playerData;
    [SerializeField] private Vector3 _closedScale = new Vector3(0.05f, 0.02f, 0.3f);
    [SerializeField] private Vector3 _openedScale = new Vector3(0.05f, 0.15f, 0.3f);
    VivoxParticipant _participant;

    private void Awake()
    {
        if (playerData == null)
            playerData = GetComponentInParent<PlayerData>();
            
                
        VivoxService.Instance.ParticipantAddedToChannel += AssignParticipant;
    }

    void AssignParticipant(VivoxParticipant participant)
    {
        if (participant.PlayerId == playerData.PlayerId.Value.ToString())
            _participant = participant;
    }

    private void Update()
    {
        if (_participant == null) return;
        transform.localScale = _participant.SpeechDetected ? _openedScale : _closedScale;
    }
}
