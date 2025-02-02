using UnityEngine;
using UnityEngine.UI;

public class BabyBird : MonoBehaviour
{
    [Header("Quest")]
    [SerializeField] Quest quest;
    public Quest Quest
    {
        get => quest;
        set => quest = value;
    }

    [Header("Dialogue")]
    [SerializeField] YarnProgram dialogue;
    public YarnProgram Dialogue { get => dialogue; private set => dialogue = value; }
    [SerializeField] string startNode;
    public string StartNode { get => startNode; private set => startNode = value; }

    [Header("General")]
    public ParticleSystem ExcitedParticles;

    [Header("Patrolling")]
    public Transform[] PatrolPoints;
    public float DefaultSpeed;
    public Vector2 MinMaxPatrolTime;
    public float MaxIdleTime;

    [Header("Flocking")]
    public float FlockSpeed;

    public GameObject Player { get; private set; }
    public StateMachine BabyStateMachine { get; private set; }

    private void Start()
    {
        Player = FindObjectOfType<Player>().gameObject;
        // Player = FindObjectOfType<PlayerController>().gameObject;
        BabyStateMachine = new StateMachine();
        BabyStateMachine.ChangeState(new BabyYardPatrollingState(this));
    }

    private void Update()
    {
        BabyStateMachine.UpdateState();
    }
}