using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Runs the full Assignment 3 scene: player movement, NPC behavior, UI updates,
// line-of-sight checks, timer decisions, collisions, and end-scene loading.
public class SceneController : MonoBehaviour
{
    [Header("Scene References")]
    // Existing scene objects assigned in the Inspector.
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform npcTransform;
    [SerializeField] private Collider playerCollider;
    [SerializeField] private Collider npcCollider;

    [Header("UI")]
    // These are the already existing text objects in A3.unity.
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text conditionsText;
    [SerializeField] private TMP_Text stateMachineText;
    [SerializeField] private TMP_Text controlsText;

    [Header("Patrol")]
    // Patrol uses exactly two points and moves back and forth between them.
    [SerializeField] private Transform patrolPointA;
    [SerializeField] private Transform patrolPointB;
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float patrolArrivalDistance = 0.15f;

    [Header("Movement")]
    // Player and chase movement happen on the XZ plane only.
    [SerializeField] private float playerMoveSpeed = 6f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float rotationSpeed = 720f;

    [Header("Decision Timer")]
    // The timer only matters while the NPC is in Idle or Patrol.
    [SerializeField] private float decisionInterval = 3f;
    [SerializeField] [Range(0f, 1f)] private float randomSwitchChance = 0.5f;

    [Header("Line Of Sight")]
    // These values control both detection and the visible LOS indicator.
    [SerializeField] private float lineOfSightDistance = 12f;
    [SerializeField] [Range(1f, 180f)] private float lineOfSightAngle = 45f;
    [SerializeField] private float lineOfSightHeight = 0.8f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    [SerializeField] private LineRenderer lineOfSightRenderer;
    [SerializeField] private Color idleSightColor = Color.white;
    [SerializeField] private Color patrolSightColor = Color.green;
    [SerializeField] private Color chaseSightColor = Color.red;

    [Header("Audio")]
    // One-shots for state changes and collision outcomes.
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip stateChangeClip;
    [SerializeField] private AudioClip collisionClip;
    [SerializeField] private float endSceneDelay = 0.25f;

    [Header("Scene Names")]
    // End scenes are loaded by name so the user can create them manually.
    [SerializeField] private string victorySceneName = "A3_Victory";
    [SerializeField] private string defeatSceneName = "A3_Defeat";

    // Runtime state used by the timer, failsafe, patrol loop, and chase logic.
    private StateMachine stateMachine;
    private float decisionTimer;
    private int consecutiveStayResults;
    private bool pendingIdleSwitch;
    private bool pendingPatrolSwitch;
    private bool patrolDecisionQueued;
    private bool playerInSight;
    private bool isEndingScene;
    private int patrolTargetIndex;
    private float playerY;
    private float npcY;

    private StateMachine.NpcState CurrentState => stateMachine?.CurrentState?.StateType ?? StateMachine.NpcState.Idle;
    private string LatestTimerResult { get; set; } = "Waiting for first timer decision";

    private void Awake()
    {
        // Build the FSM once before the scene starts updating.
        BuildStateMachine();
    }

    private void Start()
    {
        // Cache the original Y values so movement stays flat on the ground plane.
        if (playerTransform != null)
        {
            playerY = playerTransform.position.y;
        }

        if (npcTransform != null)
        {
            npcY = npcTransform.position.y;
            // The assignment requires the NPC to begin facing away from the player.
            FaceNpcAwayFromPlayer();
        }

        stateMachine.Reset();

        InitializeUI();
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (stateMachine != null)
        {
            stateMachine.OnStateChanged -= HandleStateChanged;
        }
    }

    private void Update()
    {
        // Stop all gameplay once an end scene is already loading.
        if (isEndingScene)
        {
            return;
        }

        HandlePlayerMovement();
        UpdateLineOfSight();
        UpdateDecisionTimer();
        stateMachine.Update();
        CheckCollisionResult();

        RefreshUI();
    }

    private string GetTimerDisplay()
    {
        // The timer is paused during chase because random switching no longer applies.
        if (CurrentState == StateMachine.NpcState.MoveTowardsPlayer)
        {
            return "Paused during chase";
        }

        return $"{Mathf.Max(0f, decisionTimer):F1}s";
    }

    private string GetFailsafeDisplay()
    {
        // Show how close the NPC is to a forced switch after repeated "stay" results.
        int forcedOnOpportunity = Mathf.Clamp(3 - consecutiveStayResults, 1, 3);

        if (CurrentState == StateMachine.NpcState.MoveTowardsPlayer)
        {
            return "Not used in MoveTowardsPlayer";
        }

        if (patrolDecisionQueued)
        {
            return $"Timer expired, waiting for patrol point. Forced switch on chance {forcedOnOpportunity} if needed.";
        }

        return $"Consecutive stays: {consecutiveStayResults}. Forced switch on chance {forcedOnOpportunity} if needed.";
    }

    private string GetPossibleTransitionsText()
    {
        // Keeps the state machine visualization text simple and always visible.
        switch (CurrentState)
        {
            case StateMachine.NpcState.Idle:
                return "- Player enters line of sight -> MoveTowardsPlayer\n- Timer result can switch -> Patrol\n- Timer result can stay in Idle";

            case StateMachine.NpcState.Patrol:
                return "- Player enters line of sight -> MoveTowardsPlayer\n- Timer result can switch -> Idle\n- Timer result can stay in Patrol";

            case StateMachine.NpcState.MoveTowardsPlayer:
                return "- Collision with player -> Defeat scene";

            default:
                return "- No transitions available";
        }
    }

    private void BuildStateMachine()
    {
        // The assignment only needs three NPC states.
        State idleState = new State(StateMachine.NpcState.Idle);
        State patrolState = new State(StateMachine.NpcState.Patrol);
        State moveTowardsPlayerState = new State(StateMachine.NpcState.MoveTowardsPlayer);

        // Entry actions reset timer-related data whenever the NPC changes state.
        idleState.OnEnter = () =>
        {
            ClearDecisionSwitchFlags();
            patrolDecisionQueued = false;
            decisionTimer = decisionInterval;
        };

        patrolState.OnEnter = () =>
        {
            ClearDecisionSwitchFlags();
            patrolDecisionQueued = false;
            decisionTimer = decisionInterval;
            EnsurePatrolTargetIsValid();
        };

        moveTowardsPlayerState.OnEnter = () =>
        {
            ClearDecisionSwitchFlags();
            patrolDecisionQueued = false;
            decisionTimer = 0f;
        };

        patrolState.OnUpdate = UpdatePatrolMovement;
        moveTowardsPlayerState.OnUpdate = UpdateChaseMovement;

        // LOS can interrupt Idle or Patrol immediately.
        idleState.AddTransition(new Transition(
            () => playerInSight,
            moveTowardsPlayerState));

        // Timer-based switching only happens between Idle and Patrol.
        idleState.AddTransition(new Transition(
            () => pendingPatrolSwitch,
            patrolState));

        patrolState.AddTransition(new Transition(
            () => playerInSight,
            moveTowardsPlayerState));

        patrolState.AddTransition(new Transition(
            () => pendingIdleSwitch,
            idleState));

        stateMachine = new StateMachine(idleState);
        stateMachine.OnStateChanged += HandleStateChanged;
    }

    private void HandlePlayerMovement()
    {
        // Uses the Input System keyboard and supports both WASD and arrow keys.
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || playerTransform == null)
        {
            return;
        }

        Vector2 input = Vector2.zero;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            input.x -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            input.x += 1f;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            input.y += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            input.y -= 1f;
        }

        if (input.sqrMagnitude <= 0f)
        {
            return;
        }

        Vector3 moveDirection = new Vector3(input.x, 0f, input.y).normalized;
        playerTransform.position += moveDirection * playerMoveSpeed * Time.deltaTime;
        playerTransform.position = new Vector3(playerTransform.position.x, playerY, playerTransform.position.z);
        // The player always faces the direction of travel.
        FaceInDirection(playerTransform, moveDirection);
    }

    private void UpdateDecisionTimer()
    {
        // The timer only runs while the NPC is not chasing the player.
        if (CurrentState != StateMachine.NpcState.Idle && CurrentState != StateMachine.NpcState.Patrol)
        {
            return;
        }

        if (decisionTimer > 0f)
        {
            decisionTimer = Mathf.Max(0f, decisionTimer - Time.deltaTime);

            if (decisionTimer > 0f)
            {
                return;
            }

            if (CurrentState == StateMachine.NpcState.Idle)
            {
                // Idle can evaluate immediately when the timer finishes.
                EvaluateTimerDecision();
            }
            else
            {
                // Patrol must finish the current segment before reevaluating.
                patrolDecisionQueued = true;
            }
        }
    }

    private void EvaluateTimerDecision()
    {
        // The random stay/switch choice is inline now, with a forced switch on chance 3.
        if (CurrentState != StateMachine.NpcState.Idle && CurrentState != StateMachine.NpcState.Patrol)
        {
            return;
        }

        bool forcedSwitch = consecutiveStayResults >= 2;
        bool randomSwitch = Random.value < randomSwitchChance;
        bool shouldSwitch = forcedSwitch || randomSwitch;

        if (!shouldSwitch)
        {
            consecutiveStayResults++;
            decisionTimer = decisionInterval;
            LatestTimerResult = $"Random choice stayed in {CurrentState}";
            return;
        }

        consecutiveStayResults = 0;
        string resultReason = forcedSwitch ? "Failsafe forced a switch" : "Random choice switched states";

        if (CurrentState == StateMachine.NpcState.Idle)
        {
            pendingPatrolSwitch = true;
            LatestTimerResult = $"{resultReason} (Idle -> Patrol)";
        }
        else
        {
            pendingIdleSwitch = true;
            LatestTimerResult = $"{resultReason} (Patrol -> Idle)";
        }

        stateMachine.TryTransition();
    }

    private void UpdatePatrolMovement()
    {
        // Patrol moves toward one point at a time, then swaps to the other point.
        Transform patrolTarget = GetCurrentPatrolTarget();
        if (npcTransform == null || patrolTarget == null)
        {
            return;
        }

        Vector3 currentPosition = npcTransform.position;
        Vector3 targetPosition = new Vector3(patrolTarget.position.x, npcY, patrolTarget.position.z);
        Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, patrolSpeed * Time.deltaTime);
        Vector3 travelDirection = nextPosition - currentPosition;

        npcTransform.position = nextPosition;
        FaceInDirection(npcTransform, travelDirection);

        float remainingDistance = Vector3.Distance(
            new Vector3(nextPosition.x, 0f, nextPosition.z),
            new Vector3(targetPosition.x, 0f, targetPosition.z));

        if (remainingDistance > patrolArrivalDistance)
        {
            return;
        }

        npcTransform.position = targetPosition;

        if (patrolDecisionQueued)
        {
            patrolDecisionQueued = false;
            // Delayed reevaluation happens once the current patrol leg is complete.
            EvaluateTimerDecision();
            return;
        }

        AdvancePatrolTarget();
    }

    private void UpdateChaseMovement()
    {
        // Chase always moves directly toward the player's current position.
        if (playerTransform == null || npcTransform == null)
        {
            return;
        }

        Vector3 currentPosition = npcTransform.position;
        Vector3 targetPosition = new Vector3(playerTransform.position.x, npcY, playerTransform.position.z);
        Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, chaseSpeed * Time.deltaTime);
        Vector3 travelDirection = nextPosition - currentPosition;

        npcTransform.position = nextPosition;
        FaceInDirection(npcTransform, travelDirection);
    }

    private void UpdateLineOfSight()
    {
        // LOS uses distance, view angle, and an optional raycast blocker check.
        Vector3 sightOrigin = GetSightOrigin();
        Vector3 sightEndPoint = npcTransform != null
            ? sightOrigin + npcTransform.forward * lineOfSightDistance
            : sightOrigin;
        playerInSight = false;

        if (playerTransform != null && npcTransform != null)
        {
            Vector3 playerTargetPoint = playerTransform.position + Vector3.up * lineOfSightHeight;
            Vector3 toPlayer = playerTargetPoint - sightOrigin;
            float distanceToPlayer = toPlayer.magnitude;

            if (distanceToPlayer <= lineOfSightDistance)
            {
                Vector3 flatForward = Vector3.ProjectOnPlane(npcTransform.forward, Vector3.up).normalized;
                Vector3 flatToPlayer = Vector3.ProjectOnPlane(toPlayer, Vector3.up).normalized;

                if (flatForward.sqrMagnitude > 0f && flatToPlayer.sqrMagnitude > 0f)
                {
                    float angleToPlayer = Vector3.Angle(flatForward, flatToPlayer);

                    if (angleToPlayer <= lineOfSightAngle * 0.5f)
                    {
                        if (Physics.Raycast(
                                sightOrigin,
                                toPlayer.normalized,
                                out RaycastHit hit,
                                distanceToPlayer,
                                lineOfSightMask,
                                QueryTriggerInteraction.Ignore))
                        {
                            if (IsPlayerCollider(hit.collider))
                            {
                                playerInSight = true;
                                sightEndPoint = playerTargetPoint;
                            }
                            else
                            {
                                sightEndPoint = hit.point;
                            }
                        }
                        else
                        {
                            playerInSight = true;
                            sightEndPoint = playerTargetPoint;
                        }
                    }
                }
            }
        }

        UpdateLineOfSightVisuals(sightOrigin, sightEndPoint);
    }

    private void UpdateLineOfSightVisuals(Vector3 sightOrigin, Vector3 sightEndPoint)
    {
        // The LOS visual is just a LineRenderer in this final version.
        Color currentSightColor = GetLineOfSightColor();

        if (lineOfSightRenderer != null)
        {
            lineOfSightRenderer.positionCount = 2;
            lineOfSightRenderer.SetPosition(0, sightOrigin);
            lineOfSightRenderer.SetPosition(1, sightEndPoint);
            lineOfSightRenderer.startColor = currentSightColor;
            lineOfSightRenderer.endColor = currentSightColor;
        }
    }

    private Color GetLineOfSightColor()
    {
        switch (CurrentState)
        {
            case StateMachine.NpcState.Idle:
                return idleSightColor;

            case StateMachine.NpcState.Patrol:
                return patrolSightColor;

            case StateMachine.NpcState.MoveTowardsPlayer:
                return chaseSightColor;

            default:
                return Color.white;
        }
    }

    private void InitializeUI()
    {
        if (controlsText != null)
        {
            controlsText.text = "Move: WASD / Arrow Keys";
        }
    }

    private void RefreshUI()
    {
        if (statusText != null)
        {
            statusText.text =
                $"Current State: <b>{CurrentState}</b>\n" +
                $"Timer: {GetTimerDisplay()}\n" +
                $"Latest Timer Result: {LatestTimerResult}";
        }

        if (conditionsText != null)
        {
            conditionsText.text =
                "Possible Transitions:\n" +
                $"{GetPossibleTransitionsText()}\n\n" +
                $"Player In LOS: {FormatBool(playerInSight)}\n" +
                $"Failsafe: {GetFailsafeDisplay()}";
        }

        if (stateMachineText != null)
        {
            stateMachineText.text = BuildStateMachineText();
        }
    }

    private string BuildStateMachineText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("State Machine");
        AppendStateLine(builder, StateMachine.NpcState.Idle);
        AppendStateLine(builder, StateMachine.NpcState.Patrol);
        AppendStateLine(builder, StateMachine.NpcState.MoveTowardsPlayer);
        return builder.ToString().TrimEnd();
    }

    private void AppendStateLine(StringBuilder builder, StateMachine.NpcState state)
    {
        string prefix = CurrentState == state ? "[ACTIVE]" : "[     ]";
        builder.AppendLine($"{prefix} {state}");
    }

    private string FormatBool(bool value)
    {
        return value ? "<color=#7CFC00>Yes</color>" : "<color=#FF7A7A>No</color>";
    }

    private void CheckCollisionResult()
    {
        // Collision means defeat during chase, otherwise victory.
        if (playerCollider == null || npcCollider == null)
        {
            return;
        }

        if (!playerCollider.bounds.Intersects(npcCollider.bounds))
        {
            return;
        }

        string targetScene = CurrentState == StateMachine.NpcState.MoveTowardsPlayer ? defeatSceneName : victorySceneName;
        StartCoroutine(LoadEndSceneAfterDelay(targetScene));
    }

    private IEnumerator LoadEndSceneAfterDelay(string sceneName)
    {
        // Small delay lets the collision sound play before the scene changes.
        if (isEndingScene)
        {
            yield break;
        }

        isEndingScene = true;
        PlayClip(collisionClip);

        if (endSceneDelay > 0f)
        {
            yield return new WaitForSeconds(endSceneDelay);
        }

        SceneManager.LoadScene(sceneName);
    }

    private void HandleStateChanged(State previousState, State nextState, Transition transition)
    {
        // A simple shared sound effect is enough for every state change.
        PlayClip(stateChangeClip);
    }

    private void PlayClip(AudioClip clip)
    {
        // Falls back to an AudioSource on the same GameObject if one was not assigned.
        if (clip == null)
        {
            return;
        }

        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }

        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    private void FaceNpcAwayFromPlayer()
    {
        // Used only once at startup to satisfy the initial facing requirement.
        if (npcTransform == null || playerTransform == null)
        {
            return;
        }

        Vector3 awayDirection = npcTransform.position - playerTransform.position;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude > 0f)
        {
            npcTransform.rotation = Quaternion.LookRotation(awayDirection.normalized, Vector3.up);
        }
    }

    private void FaceInDirection(Transform target, Vector3 direction)
    {
        // Rotates only around Y so the capsules do not tip over.
        if (target == null)
        {
            return;
        }

        Vector3 flatDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (flatDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        target.rotation = Quaternion.RotateTowards(target.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void EnsurePatrolTargetIsValid()
    {
        // If the NPC starts on top of one patrol point, switch to the opposite one.
        Transform patrolTarget = GetCurrentPatrolTarget();
        if (npcTransform == null || patrolTarget == null)
        {
            return;
        }

        float distanceToTarget = Vector3.Distance(
            new Vector3(npcTransform.position.x, 0f, npcTransform.position.z),
            new Vector3(patrolTarget.position.x, 0f, patrolTarget.position.z));

        if (distanceToTarget <= patrolArrivalDistance)
        {
            AdvancePatrolTarget();
        }
    }

    private Transform GetCurrentPatrolTarget()
    {
        if (patrolPointA == null || patrolPointB == null)
        {
            return null;
        }

        return patrolTargetIndex == 0 ? patrolPointA : patrolPointB;
    }

    private void AdvancePatrolTarget()
    {
        patrolTargetIndex = patrolTargetIndex == 0 ? 1 : 0;
    }

    private void ClearDecisionSwitchFlags()
    {
        // Only one timer-driven transition should ever be pending at a time.
        pendingIdleSwitch = false;
        pendingPatrolSwitch = false;
    }

    private Vector3 GetSightOrigin()
    {
        if (npcTransform == null)
        {
            return Vector3.zero;
        }

        return npcTransform.position + Vector3.up * lineOfSightHeight;
    }

    private bool IsPlayerCollider(Collider other)
    {
        // Accepts the assigned player collider or any child collider on the player object.
        if (other == null)
        {
            return false;
        }

        if (playerCollider != null && other == playerCollider)
        {
            return true;
        }

        if (playerTransform == null)
        {
            return false;
        }

        return other.transform == playerTransform || other.transform.IsChildOf(playerTransform);
    }
}
