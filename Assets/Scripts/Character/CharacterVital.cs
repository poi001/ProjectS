using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 플레이어/적 공용. 캐릭터의 현재 체력, 피격, 사망을 담당한다. 데미지를 "받는" 쪽만 처리하고, 주는 쪽은 공격 로직이 담당한다.
// 최대 체력은 각 캐릭터(Player, Enemy)가 초기화할 때 스탯에서 받아온다.
// 승부 규칙 (GDD 6.4)
//  - 상대가 죽으면(SetOpponentDead) 이 캐릭터는 피해로 체력이 1 아래로 내려가지 않는다. (이미 1보다 낮으면 그 체력 밑으로 내려가지 않는다)
//    상대의 사망은 각 캐릭터가 알려 준다. (적: 타깃 플레이어의 Died, 플레이어: 보스의 CharacterDiedEvent)
//  - 사망을 미루는 캐릭터(플레이어, SetDefersDeath)는 치명 피해를 입어도 프레임 끝까지 사망 확정을 미룬다.
//    같은 프레임에 상대(보스)도 죽으면 플레이어가 이기므로 죽지 않은 것으로 한다.
public class CharacterVital : MonoBehaviour, IDamageable
{
    // 상대가 죽은 뒤 피해로 내려갈 수 있는 최소 체력
    private const float OpponentDeadMinimumHealth = 1f;

    // 무적이 끝나는 시각. Time.time 기준이라 타임스케일이 0이면(히트스톱 등) 무적 시간도 함께 멈춘다.
    private float _invincibleEndTime = float.NegativeInfinity;

    // 상대가 죽었는지 여부. 켜지면 피해로 체력이 1 아래로 내려가지 않는다.
    private bool _isOpponentDead;
    // 치명 피해를 입었을 때 사망 확정을 프레임 끝까지 미루는지 여부. (플레이어가 켠다)
    private bool _defersDeath;
    // 치명 피해를 입고 사망 확정을 프레임 끝까지 미루는 중인지 여부. 이 동안 IsDead는 false다. (치명 피해를 알리는 Damaged 시점만 true)
    private bool _isDeathPending;
    // 사망 확정을 미룬 치명 피해를 입기 직전의 체력. 같은 프레임에 상대가 죽어 살아남을 때 이 값을 기준으로 체력을 되돌린다.
    private float _healthBeforeLethalDamage;
    // 사망 확정을 미룬 치명 피해의 정보. 살아남았을 때 DeathAverted로 넘긴다.
    private DamageInfo _lethalDamageInfo;

    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    // 체력을 모두 잃었더라도 소진 상태(IsDepleted)이거나 사망 확정을 프레임 끝까지 미루는 중(_isDeathPending)에는 죽은 것으로 보지 않는다.
    // 단, 치명 피해를 알리는 Damaged 구독자는 그 피격을 치명 피해(IsDead)로 본다. (대기는 Damaged가 끝난 뒤에 시작한다)
    public bool IsDead => CurrentHealth <= 0f && !IsDepleted && !_isDeathPending;
    // 치명 피해를 입고 사망 확정을 프레임 끝까지 미루는 중인지 여부. 이 프레임이 끝나면 죽거나(Died) 살아남는다(DeathAverted).
    // 상대(적)가 이 캐릭터를 이미 죽은 것으로 봐야 할 때는 IsDead와 함께 확인한다. (Enemy.IsTargetDead)
    public bool IsDeathPending => _isDeathPending;
    // 체력을 모두 잃어도 죽지 않고 소진 상태가 되는지 여부. 기본은 꺼져 있어 체력이 0이 되면 죽는다. (다음 페이즈가 남은 보스가 켠다)
    public bool SurvivesDepletion { get; private set; }
    // 소진 상태. 체력을 모두 잃었지만 죽지 않고 다시 채워지기를 기다리는 중이다. (보스 페이즈 전환)
    // 이 동안에는 피해도 회복도 받지 않으며, 맞아도 피해 이벤트·데미지 숫자·피격음이 발생하지 않는다. EndDepletion으로 끝낸다.
    public bool IsDepleted { get; private set; }
    // 무적 중에는 피해를 입지 않는다. (피해 이벤트, 데미지 숫자, 피격음도 발생하지 않는다.)
    public bool IsInvincible => Time.time < _invincibleEndTime;
    // 피해 무효 중에는 맞기는 하지만 체력이 줄지 않는다. (SoulKnight의 기도 등)
    // 무적과 달리 맞은 것으로 처리해 히트 이펙트와 0 데미지 숫자가 뜨고, Damaged 대신 DamageNullified가 발생한다.
    public bool IsDamageNullified { get; private set; }
    // 피해로 체력이 이 값 아래로 내려가지 않는다. 기본 0이며, 0보다 크면 피해로는 죽지 않는다. (SoulKnight의 각성 전 보호 등)
    // 회복이나 체력 직접 설정에는 영향을 주지 않는다.
    public float MinimumHealth { get; private set; }
    // 피해로 내려갈 수 있는 실제 최소 체력. 상대가 죽었으면 최소 체력(MinimumHealth)과 1 중 높은 쪽이다.
    private float HealthFloor => _isOpponentDead ? Mathf.Max(MinimumHealth, OpponentDeadMinimumHealth) : MinimumHealth;

    public event Action<DamageInfo> Damaged;
    // 피해 무효 중에 맞았을 때 호출된다. 체력은 그대로이며 Damaged는 호출되지 않는다. (피해 무효 전용 피격음 등)
    public event Action<DamageInfo> DamageNullified;
    public event Action Died;
    // 사망 확정을 미루던 치명 피해가 같은 프레임의 상대 사망으로 취소되어 살아남았을 때 호출된다. (그 피격의 정보를 넘긴다) Died는 발생하지 않는다.
    // 치명 피해를 알린 Damaged에서는 피격 반응을 하지 않았으므로, 살아남은 쪽이 이 시점에 피격 반응을 한다.
    public event Action<DamageInfo> DeathAverted;
    // 체력을 모두 잃었지만 죽지 않고 소진 상태가 되었을 때 호출된다. (SurvivesDepletion이 켜져 있을 때) Died와 CharacterDiedEvent는 발생하지 않는다.
    public event Action Depleted;
    // 초기화·피해·회복·충전으로 체력이 바뀔 때마다 호출된다. (체력바 등 이 캐릭터를 직접 참조하는 UI용) 바뀐 값은 CurrentHealth/MaxHealth로 읽는다.
    public event Action HealthChanged;

    public void Initialize(float maxHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        _invincibleEndTime = float.NegativeInfinity;
        IsDamageNullified = false;
        MinimumHealth = 0f;
        SurvivesDepletion = false;
        IsDepleted = false;
        _isOpponentDead = false;
        _defersDeath = false;
        _isDeathPending = false;
        HealthChanged?.Invoke();
    }

    // 상대가 죽었음을 알린다. 이제부터 피해로 체력이 1 아래로 내려가지 않는다. 상대의 사망을 받은 캐릭터(Player, Enemy)가 호출한다.
    public void SetOpponentDead()
    {
        _isOpponentDead = true;
    }

    // 치명 피해를 입었을 때 사망 확정을 프레임 끝까지 미룰지 정한다. 같은 프레임의 동시 사망을 플레이어 승리로 판정하기 위해 플레이어가 켠다.
    public void SetDefersDeath(bool defersDeath)
    {
        _defersDeath = defersDeath;
    }

    // 체력을 모두 잃었을 때 죽는 대신 소진 상태가 되게 할지 정한다. 마지막 페이즈처럼 실제로 죽어야 할 때는 끈다.
    public void SetSurvivesDepletion(bool survivesDepletion)
    {
        SurvivesDepletion = survivesDepletion;
    }

    // 소진 상태에서 체력을 채워 넣는다. (페이즈 전환의 체력 충전 연출) 최대 체력을 maxHealth로 바꾸고 그중 ratio(0 ~ 1)만큼 채운다.
    // 회복이 아니므로 회복 숫자(HealEvent)는 띄우지 않는다. 소진 상태가 아니면 아무것도 하지 않는다.
    public void SetRefillHealth(float maxHealth, float ratio)
    {
        if (!IsDepleted)
        {
            return;
        }

        MaxHealth = maxHealth;
        CurrentHealth = maxHealth * Mathf.Clamp01(ratio);
        HealthChanged?.Invoke();
    }

    // 소진 상태를 끝낸다. 최대 체력을 maxHealth로 바꿔 가득 채우고, 다시 피해를 입을 수 있게 한다. 소진 상태가 아니면 아무것도 하지 않는다.
    public void EndDepletion(float maxHealth)
    {
        if (!IsDepleted)
        {
            return;
        }

        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        IsDepleted = false;
        HealthChanged?.Invoke();
    }

    // 피해로 내려갈 수 있는 최소 체력을 정한다. 0이면 제한 없이 피해로 죽을 수 있다.
    // 최소 체력에 걸려 깎이지 않은 피해도 맞은 것으로 처리된다. (피해량은 실제로 깎인 양인 0으로 알린다)
    public void SetMinimumHealth(float minimumHealth)
    {
        MinimumHealth = Mathf.Max(0f, minimumHealth);
    }

    // 피해 무효를 켜거나 끈다. 켠 쪽(상태 등)이 끝날 때 반드시 끈다.
    public void SetDamageNullified(bool isNullified)
    {
        IsDamageNullified = isNullified;
    }

    // 지금부터 duration초 동안 무적으로 만든다. 이미 더 길게 남은 무적이 있다면 그대로 둔다.
    public void SetInvincible(float duration)
    {
        _invincibleEndTime = Mathf.Max(_invincibleEndTime, Time.time + duration);
    }

    public bool TakeDamage(DamageInfo damageInfo)
    {
        if (IsDead || IsDepleted || _isDeathPending || IsInvincible || damageInfo.Amount <= 0f)
        {
            return false;
        }

        // 맞은 것으로 처리하되 피해는 0으로 알린다. (히트 이펙트와 0 데미지 숫자는 DamageTakenEvent 구독자가 띄운다)
        if (IsDamageNullified)
        {
            DamageNullified?.Invoke(damageInfo);
            EventBus<DamageTakenEvent>.Publish(new DamageTakenEvent(gameObject, damageInfo.Instigator, 0f, damageInfo.HitPoint));
            return true;
        }

        float previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Max(Mathf.Min(previousHealth, HealthFloor), previousHealth - damageInfo.Amount);
        HealthChanged?.Invoke();
        Damaged?.Invoke(damageInfo);

        // 연출 시스템(데미지 숫자 등)용. 체력보다 큰 피해는 실제로 깎인 양만 알린다.
        EventBus<DamageTakenEvent>.Publish(new DamageTakenEvent(gameObject, damageInfo.Instigator, previousHealth - CurrentHealth, damageInfo.HitPoint));

        // 위의 Damaged 구독자는 체력을 모두 잃은 이 피격을 치명 피해(IsDead)로 본다. 소진 상태는 그 뒤에 시작한다.
        if (IsDead)
        {
            if (SurvivesDepletion)
            {
                IsDepleted = true;
                Depleted?.Invoke();
            }
            else if (_defersDeath)
            {
                // 같은 프레임에 상대(보스)도 죽으면 플레이어가 이기므로, 사망 확정을 프레임 끝까지 미룬다.
                _isDeathPending = true;
                _healthBeforeLethalDamage = previousHealth;
                _lethalDamageInfo = damageInfo;
                ResolvePendingDeathAsync(destroyCancellationToken).Forget();
            }
            else
            {
                AnnounceDeath();
            }
        }

        return true;
    }

    // 프레임 끝에서 사망을 확정한다. 그 사이(같은 프레임) 상대가 죽었다면 이 캐릭터가 이기므로 죽지 않은 것으로 한다.
    private async UniTaskVoid ResolvePendingDeathAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, cancellationToken);

        // 그 사이 Initialize 등으로 이미 정리되었다면 할 일이 없다.
        if (!_isDeathPending)
        {
            return;
        }

        _isDeathPending = false;

        if (_isOpponentDead)
        {
            // 상대가 먼저 죽은 것으로 보고, 치명 피해가 상대 사망 후 보호(1)에 걸린 것처럼 체력을 맞춘다.
            CurrentHealth = Mathf.Min(_healthBeforeLethalDamage, HealthFloor);
            HealthChanged?.Invoke();
            DeathAverted?.Invoke(_lethalDamageInfo);
            return;
        }

        AnnounceDeath();
    }

    private void AnnounceDeath()
    {
        Died?.Invoke();
        // 연출 시스템(KO 연출 등)용. 사망 상태 전환까지 끝난 뒤에 알린다.
        EventBus<CharacterDiedEvent>.Publish(new CharacterDiedEvent(gameObject));
    }

    public bool Heal(float healAmount)
    {
        if (IsDead || IsDepleted || _isDeathPending || healAmount <= 0f || CurrentHealth >= MaxHealth)
        {
            return false;
        }

        float previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + healAmount);
        HealthChanged?.Invoke();

        // 연출 시스템(회복 숫자 등)용. 최대 체력을 넘는 회복량은 실제로 오른 양만 알린다.
        EventBus<HealEvent>.Publish(new HealEvent(gameObject, CurrentHealth - previousHealth, transform.position));

        return true;
    }
}
