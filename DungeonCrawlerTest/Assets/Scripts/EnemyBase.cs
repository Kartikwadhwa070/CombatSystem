using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;
using System.Collections;

public class EnemyBase : MonoBehaviour
{
    [Space]
    [Header("Components")]
    [SerializeField] private Animator anim;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Rigidbody rb;

    [Space]
    [Header("Combat")]
    [SerializeField] private Transform attackPos;
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private int quickAttackDamage = 10;
    [SerializeField] private int heavyAttackDamage = 20;

    [Space]
    [Header("Movement")]
    [SerializeField] private float minSpeedToAnimate = 0.1f;
    [SerializeField] private string speedParameterName = "Speed";
    [SerializeField] private float stoppingDistance = 2f;

    [Space]
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showAnimationLogs = true;
    [SerializeField] private bool showCombatLogs = true;
    [SerializeField] private bool showMovementDebug = true;
    [SerializeField] private Color debugTextColor = Color.yellow;

    [Space]
    [Header("Combat Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject targetIndicator;

    // Private variables
    private Transform player;
    private PlayerHealth playerHealth;
    private bool isAttacking;
    private bool isDead;
    private float lastAttackTime;
    private bool isTargeted;
    private string currentAnimationState = "Idle";

    private void OnValidate()
    {
        // Auto-fetch components if they exist
        if (!anim) anim = GetComponent<Animator>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!rb) rb = GetComponent<Rigidbody>();

        // Verify animator parameters
        if (anim)
        {
            foreach (AnimatorControllerParameter param in anim.parameters)
            {
                if (param.name == speedParameterName)
                {
                    Debug.Log($"<color=green>Found {speedParameterName} parameter in Animator</color>");
                    return;
                }
            }
            Debug.LogError($"<color=red>Missing {speedParameterName} parameter in Animator!</color>");
        }

        // Create attack position if it doesn't exist
        if (!attackPos)
        {
            GameObject attackPosObj = new GameObject("AttackPos");
            attackPosObj.transform.parent = transform;
            attackPosObj.transform.localPosition = new Vector3(0, 0, 1); // 1 unit in front
            attackPos = attackPosObj.transform;
        }
    }

    private void Start()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // Setup NavMeshAgent
        if (agent)
        {
            agent.stoppingDistance = stoppingDistance;
            agent.updateRotation = true;
            agent.updatePosition = true;
        }

        // Find player
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (!player && showDebugLogs)
        {
            Debug.LogError($"<color=red>[{gameObject.name}] Player not found! Make sure player has 'Player' tag.</color>");
            return;
        }

        playerHealth = player.GetComponent<PlayerHealth>();
        if (!playerHealth && showDebugLogs)
        {
            Debug.LogError($"<color=red>[{gameObject.name}] PlayerHealth component not found on player!</color>");
        }

        if (targetIndicator) targetIndicator.SetActive(false);

        if (showDebugLogs)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] Initialized with:" +
                $"\n- Attack Range: {attackRange}" +
                $"\n- Detection Range: {detectionRange}" +
                $"\n- Attack Cooldown: {attackCooldown}" +
                $"\n- Quick Attack Damage: {quickAttackDamage}" +
                $"\n- Heavy Attack Damage: {heavyAttackDamage}</color>");
        }
    }

    private void Update()
    {
        if (isDead || !player) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (showDebugLogs && showCombatLogs)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] " +
                $"Distance to player: {distanceToPlayer:F2}, " +
                $"Can Attack: {!isAttacking && Time.time >= lastAttackTime + attackCooldown}, " +
                $"Time until next attack: {Mathf.Max(0, (lastAttackTime + attackCooldown) - Time.time):F2}</color>");
        }

        if (distanceToPlayer <= detectionRange)
        {
            if (distanceToPlayer <= attackRange && !isAttacking && Time.time >= lastAttackTime + attackCooldown)
            {
                StopMoving();
                PerformRandomAttack();
            }
            else if (!isAttacking)
            {
                ChasePlayer();
            }
        }
        else
        {
            StopMoving();
        }
    }

    private void ChasePlayer()
    {
        if (!agent || !anim || !player) return;

        agent.isStopped = false;
        agent.SetDestination(player.position);

        // Smoothly update velocity to prevent sudden jumps
        float targetSpeed = agent.velocity.magnitude;
        float smoothedSpeed = Mathf.Lerp(anim.GetFloat(speedParameterName), targetSpeed, Time.deltaTime * 5f); // Adjust smoothing factor

        // Normalize speed to match the blend tree range (0 to 6)
        float normalizedSpeed = Mathf.Clamp(smoothedSpeed / agent.speed * 6f, 0f, 6f);

        // Set animator speed parameter
        anim.SetFloat(speedParameterName, normalizedSpeed);

        if (showMovementDebug)
        {
            Debug.Log($"<color=yellow>[{gameObject.name} Movement] " +
                $"\nActual Speed: {targetSpeed:F2}" +
                $"\nSmoothed Speed: {smoothedSpeed:F2}" +
                $"\nNormalized Speed: {normalizedSpeed:F2}" +
                $"\nIs Moving: {targetSpeed > minSpeedToAnimate}" +
                $"\nDestination Distance: {agent.remainingDistance:F2}" +
                $"\nAgent State: {agent.pathStatus}</color>");
        }
    }


    private void StopMoving()
    {
        if (!agent || !anim) return;

        agent.isStopped = true;
        anim.SetFloat(speedParameterName, 0f);

        if (showMovementDebug)
        {
            Debug.Log($"<color=yellow>[{gameObject.name} Movement] Stopped moving, speed set to 0</color>");
        }
    }


    private void PerformRandomAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        RotateTowardsTarget(player.position);

        bool isHeavyAttack = Random.value >= 0.7f;
        if (isHeavyAttack)
        {
            PerformHeavyAttack();
        }
        else
        {
            PerformQuickAttack();
        }

        if (showDebugLogs && showCombatLogs)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] " +
                $"Performing {(isHeavyAttack ? "Heavy" : "Quick")} Attack</color>");
        }
    }

    private void PerformQuickAttack()
    {
        int attackIndex = Random.Range(1, 4);
        string attackName = "";

        switch (attackIndex)
        {
            case 1:
                anim.SetBool("punch", true);
                attackName = "Punch";
                break;
            case 2:
                anim.SetBool("kick", true);
                attackName = "Kick";
                break;
            case 3:
                anim.SetBool("mmakick", true);
                attackName = "MMA Kick";
                break;
        }

        if (showDebugLogs && showAnimationLogs)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] " +
                $"Playing {attackName} animation</color>");
        }
    }

    private void PerformHeavyAttack()
    {
        int attackIndex = Random.Range(1, 3);
        string attackName = $"Heavy Attack {attackIndex}";

        switch (attackIndex)
        {
            case 1:
                anim.SetBool("heavyAttack1", true);
                break;
            case 2:
                anim.SetBool("heavyAttack2", true);
                break;
        }

        if (showDebugLogs && showAnimationLogs)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] " +
                $"Playing {attackName} animation</color>");
        }
    }

    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

            if (showDebugLogs)
            {
                Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] " +
                    $"Rotating towards player, angle: {Quaternion.Angle(transform.rotation, targetRotation):F2}</color>");
            }
        }
    }

    // Called by animation events
    public void PerformAttack()
    {
        if (showDebugLogs && showCombatLogs)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] " +
                $"Checking for hits within range {attackRange}</color>");
        }

        Collider[] hitPlayers = Physics.OverlapSphere(attackPos.position, attackRange, playerLayer);

        foreach (Collider playerCol in hitPlayers)
        {
            if (playerCol.TryGetComponent(out PlayerHealth health))
            {
                int damage = anim.GetBool("heavyAttack1") || anim.GetBool("heavyAttack2") 
                    ? heavyAttackDamage 
                    : quickAttackDamage;

                health.TakeDamage(damage);
                SpawnHitEffect(playerCol.transform.position);

                if (showDebugLogs && showCombatLogs)
                {
                    Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] " +
                        $"Hit player for {damage} damage!</color>");
                }
            }
        }
    }

    // Called by animation events
    public void ResetAttack()
    {
        anim.SetBool("punch", false);
        anim.SetBool("kick", false);
        anim.SetBool("mmakick", false);
        anim.SetBool("heavyAttack1", false);
        anim.SetBool("heavyAttack2", false);
        isAttacking = false;

        if (showDebugLogs && showAnimationLogs)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] Reset all attack animations</color>");
        }
    }

    public void TakeDamage(int damage)
    {
        if (showDebugLogs && showCombatLogs)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] Took {damage} damage!</color>");
        }

        anim.SetTrigger("Hit");
        SpawnHitVfx(transform.position);
    }

    public void Die()
    {
        isDead = true;
        agent.isStopped = true;
        anim.SetTrigger("Die");
        
        GetComponent<Collider>().enabled = false;
        rb.isKinematic = true;

        if (showDebugLogs)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] Enemy died</color>");
        }
        
        Destroy(gameObject, 3f);
    }

    public void ActiveTarget(bool active)
    {
        isTargeted = active;
        if (targetIndicator)
        {
            targetIndicator.SetActive(active);
        }

        if (showDebugLogs)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] " +
                $"Target indicator {(active ? "activated" : "deactivated")}</color>");
        }
    }

    public void SpawnHitVfx(Vector3 position)
    {
        if (hitEffectPrefab)
        {
            Instantiate(hitEffectPrefab, position, Quaternion.identity);
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(debugTextColor)}>[{gameObject.name}] Spawned hit VFX</color>");
            }
        }
    }

    private void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab)
        {
            Instantiate(hitEffectPrefab, position, Quaternion.identity);
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugLogs) return;

        // Attack range
        if (attackPos != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPos.position, attackRange);
        }

        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw line to player if in range
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= detectionRange)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, player.position);
            }
        }
    }
}