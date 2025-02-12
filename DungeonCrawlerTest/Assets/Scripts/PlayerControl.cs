using UnityEngine;
using DG.Tweening;
using StarterAssets;
using System.Collections;

public class PlayerControl : MonoBehaviour
{
    [Space]
    [Header("Components")]
    [SerializeField] private Animator anim;
    [SerializeField] private ThirdPersonController thirdPersonController;
    [SerializeField] private Rigidbody rb;

    [Space]
    [Header("Combat")]
    public Transform target;
    [SerializeField] private Transform attackPos;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float airKnockbackForce = 10f;
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float comboTimeWindow = 0.5f;
    [SerializeField] private float attackCooldown = 0.2f;
    [SerializeField] private float rotationSpeed = 15f;

    [Space]
    [Header("Combat Effects")]
    [SerializeField] private float cameraShakeIntensity = 0.5f;
    [SerializeField] private float cameraShakeDuration = 0.1f;
    [SerializeField] private GameObject hitEffectPrefab;

    [Space]
    [Header("Debug")]
    [SerializeField] private bool debug;

    // Private variables
    private bool isAttacking;
    private bool canCombo;
    private float lastAttackTime;
    private int comboCount;
    private Coroutine resetComboCoroutine;
    private EnemyBase currentTarget;
    private EnemyBase oldTarget;

    private void Start()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        if (!anim) anim = GetComponent<Animator>();
        if (!thirdPersonController) thirdPersonController = GetComponent<ThirdPersonController>();
        if (!rb) rb = GetComponent<Rigidbody>();

        comboCount = 0;
        lastAttackTime = -attackCooldown;
    }

    private void Update()
    {
        HandleCombatInput();
    }

    private void FixedUpdate()
    {
        HandleTargetTracking();
    }

    private void HandleCombatInput()
    {
        // Only check cooldown for starting new attacks, not continuing combos
        if (!isAttacking && Time.time - lastAttackTime < attackCooldown) return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J))
        {
            if (!isAttacking || canCombo)
            {
                QuickAttack();
            }
        }
        else if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.K))
        {
            if (!isAttacking)
            {
                HeavyAttack();
            }
        }
    }

    private void QuickAttack()
    {
        int attackIndex = (comboCount % 3) + 1;

        if (debug)
        {
            Debug.Log($"Performing Quick Attack: {attackIndex}");
        }

        // If we have a target, face it
        if (target != null)
        {
            RotateTowardsTarget(target.position);
        }

        switch (attackIndex)
        {
            case 1:
                anim.SetBool("punch", true);
                break;
            case 2:
                anim.SetBool("kick", true);
                break;
            case 3:
                anim.SetBool("mmakick", true);
                break;
        }

        isAttacking = true;
        comboCount++;
        lastAttackTime = Time.time;

        if (resetComboCoroutine != null)
            StopCoroutine(resetComboCoroutine);
        resetComboCoroutine = StartCoroutine(ResetComboAfterDelay());
    }

    private void HeavyAttack()
    {
        int attackIndex = Random.Range(1, 3);

        if (debug)
        {
            Debug.Log($"Performing Heavy Attack: {attackIndex}");
        }

        // If we have a target, face it
        if (target != null)
        {
            RotateTowardsTarget(target.position);
        }

        switch (attackIndex)
        {
            case 1:
                anim.SetBool("heavyAttack1", true);
                break;
            case 2:
                anim.SetBool("heavyAttack2", true);
                break;
        }

        isAttacking = true;
        lastAttackTime = Time.time;
        comboCount = 0;
    }

    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }

    private IEnumerator ResetComboAfterDelay()
    {
        yield return new WaitForSeconds(comboTimeWindow);
        comboCount = 0;
        canCombo = false;
    }

    public void PerformAttack()
    {
        Collider[] hitEnemies = Physics.OverlapSphere(attackPos.position, attackRange, enemyLayer);

        foreach (Collider enemy in hitEnemies)
        {
            if (enemy.TryGetComponent(out Rigidbody enemyRb) &&
                enemy.TryGetComponent(out EnemyBase enemyBase))
            {
                ApplyKnockback(enemyRb, enemyBase);
                SpawnHitEffect(enemy.transform.position);
                CameraShake();
            }
        }
    }

    private void ApplyKnockback(Rigidbody enemyRb, EnemyBase enemyBase)
    {
        Vector3 knockbackDirection = enemyRb.transform.position - transform.position;
        knockbackDirection.y = airKnockbackForce;

        enemyRb.linearVelocity = Vector3.zero;
        enemyRb.AddForce(knockbackDirection.normalized * knockbackForce, ForceMode.Impulse);
        enemyBase.SpawnHitVfx(enemyBase.transform.position);
    }

    private void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab)
        {
            Instantiate(hitEffectPrefab, position, Quaternion.identity);
        }
    }

    private void CameraShake()
    {
        Camera.main.transform.DOShakePosition(cameraShakeDuration, cameraShakeIntensity);
    }

    public void ResetAttack()
    {
        anim.SetBool("punch", false);
        anim.SetBool("kick", false);
        anim.SetBool("mmakick", false);
        anim.SetBool("heavyAttack1", false);
        anim.SetBool("heavyAttack2", false);

        canCombo = true;
        isAttacking = false;

        ResetCombatState();
    }

    private void ResetCombatState()
    {
        thirdPersonController.canMove = true;
        TargetDetectionControl.instance.canChangeTarget = true;
    }

    private void HandleTargetTracking()
    {
        if (target == null) return;

        if (Vector3.Distance(transform.position, target.position) >= TargetDetectionControl.instance.detectionRange)
        {
            NoTarget();
        }
    }

    public void ChangeTarget(Transform newTarget)
    {
        if (target != null)
        {
            oldTarget = currentTarget;
            oldTarget.ActiveTarget(false);
        }

        target = newTarget;
        currentTarget = newTarget.GetComponent<EnemyBase>();
        currentTarget.ActiveTarget(true);
    }

    private void NoTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.ActiveTarget(false);
            currentTarget = null;
            oldTarget = null;
            target = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPos == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPos.position, attackRange);
    }
}