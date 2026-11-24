using UnityEngine;

// 결석: 보스를 처치하면 보스 몸에서 뿜어져 나오는 보상 물체. (GDD 6.6) 종류는 리워드(기본 동작)·액티브 룬·패시브 룬 셋이다.
// 프리팹은 종류만 정하고 모든 보스가 같이 쓴다. 보스가 어떤 결석을 떨구고 그 결석이 무엇을 주는지는 보스 데이터(EnemyStatsSO)에 있다.
// 움직임: 정해 준 방향으로 뿜어져 나가고(Launch) → 중력으로 떨어지고 → 바닥·벽·천장에서 탱탱볼처럼 튕기다가 → 튀는 힘이 약해지면 멈춘다.
//  - 방향은 소환하는 쪽(BattleResultManager)이 정한다. 결석이 여럿이면 서로 다른 방향을 준다. 따로 정해 주지 않으면 똑바로 위로 솟는다.
//  - 튕김은 물리 엔진이 처리한다. (Dynamic) Interactable 레이어는 지형(Ground, Wall)과 충돌하므로 결석은 지형에 막혀 필드 밖으로 나가지 않는다. (GDD 6.2)
//    튕기는 세기는 콜라이더의 물리 재질(Bounciness)이 정한다. 이 컴포넌트는 중력 세기, 낙하 속도 상한, 바닥에서 튕길 때 줄어드는 가로 속도, 멈추는 기준만 맡는다.
//  - 결석끼리는 충돌하지 않는다. (Interactable끼리는 충돌하지 않음) 캐릭터의 몸(Character)과도 충돌하지 않아 플레이어가 밀거나 올라설 수 없다.
// 줍기: 처음 바닥에 닿은 뒤부터, 플레이어의 피격 판정(Player 레이어 트리거)이 닿으면 RewardStoneCollectedEvent를 발행하고 사라진다.
//  - 피격 판정이 트리거라서, 결석의 콜라이더가 트리거가 아니어도 겹치면 OnTrigger 알림이 온다. (밀거나 막지는 않는다)
//  - 솟아오르는 동안에는 닿아도 줍지 않는다. (보스 바로 옆에서 잡아도 튀어 오르는 모습이 보이도록)
//  - 게임이 일시정지된 동안(다른 결석의 획득 창·룬 선택 창이 열려 있는 동안)에도 줍지 않는다. 결석 두 개에 한꺼번에 닿아도 하나씩 처리되고,
//    남은 결석은 그 자리에 있다가 창이 닫힌 뒤 겹쳐 있으면 바로 이어서 주워진다. (창이 겹쳐 뜨거나 보상이 사라지지 않게)
//  - 주웠을 때 무엇을 할지(동작 해금, 룬 선택 창, 액티브 룬 지급)는 이 이벤트를 받는 BattleResultManager가 종류에 따라 처리한다.
// 프리팹 구성: 루트(Interactable 레이어)에 이 컴포넌트, Rigidbody2D(Dynamic), CircleCollider2D(트리거 아님, 물리 재질 RewardStone). 그림은 자식 Visual이 맡는다.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class RewardStone : MonoBehaviour
{
    private const string PlayerHurtboxLayerName = "Player";
    // 닿은 면의 법선 y가 이 값보다 크면 바닥으로 본다.
    private const float FloorNormalThreshold = 0.5f;

    [Header("보상")]
    [Tooltip("이 결석의 종류입니다. 주웠을 때 일어나는 일이 이 값으로 갈립니다.\nAbility(리워드): 획득 창이 뜨고 동작이 해금됩니다. ActiveRune: 창 없이 바로 액티브 룬을 얻습니다. PassiveRune: 룬 선택 창이 뜹니다.\n무엇을 주는지는 보스 데이터(EnemyStatsSO)의 처치 보상에 적습니다.")]
    [SerializeField] private RewardType _rewardType;

    [Header("뿜어져 나오기")]
    [Tooltip("나오는 순간의 속도입니다. 방향은 소환하는 쪽(BattleResultManager)이 무작위로 정해 줍니다. 클수록 멀리, 높이 날아갑니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _launchSpeed = 14f;

    [Header("낙하·튕김")]
    [Tooltip("아래로 당기는 중력의 세기입니다. 클수록 빨리 떨어집니다. 시작할 때 Rigidbody2D의 Gravity Scale로 바꿔 넣으므로 Gravity Scale은 직접 고치지 않습니다. (단위: 월드 거리/초²)\n튕기는 세기는 콜라이더의 물리 재질(Assets/Data/Physics/RewardStone)의 Bounciness에서 정합니다.")]
    [SerializeField, Min(0f)] private float _gravity = 35f;
    [Tooltip("떨어지는 속도의 최댓값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _maxFallSpeed = 25f;
    [Tooltip("바닥에서 튕길 때마다 남는 가로 속도의 비율입니다. 0이면 바닥에 닿는 순간 가로로 멈추고 1이면 줄지 않습니다. (0~1)\n물리 재질의 마찰은 0으로 두고 이 값으로만 가로 속도를 줄입니다.")]
    [SerializeField, Range(0f, 1f)] private float _floorHorizontalKeep = 0.7f;
    [Tooltip("바닥에서 튕겨 오르는 속도가 이 값보다 작아지면 튕기지 않고 그 자리에 멈춥니다. 클수록 일찍 멈춥니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _restSpeed = 2f;

    private Rigidbody2D _rigidbody;
    private int _playerHurtboxLayer;
    // 처음 바닥에 닿았는지 여부. 닿은 뒤부터 주울 수 있다.
    private bool _hasLanded;
    // 튕김이 끝나 바닥에 멈췄는지 여부
    private bool _isResting;
    private bool _isCollected;

    public RewardType RewardType => _rewardType;
    // 지금 플레이어가 닿으면 주울 수 있는지 여부
    public bool CanBeCollected => _hasLanded && !_isCollected;
    public bool IsResting => _isResting;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _playerHurtboxLayer = LayerMask.NameToLayer(PlayerHurtboxLayerName);

        // 멈춘 뒤에도 플레이어가 닿는 것을 놓치지 않도록 잠들지 않게 한다. (잠든 물체는 겹침 알림을 받지 못한다)
        _rigidbody.bodyType = RigidbodyType2D.Dynamic;
        _rigidbody.sleepMode = RigidbodySleepMode2D.NeverSleep;
        _rigidbody.freezeRotation = true;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        float worldGravity = Mathf.Abs(Physics2D.gravity.y);
        _rigidbody.gravityScale = worldGravity > Mathf.Epsilon ? _gravity / worldGravity : 0f;
    }

    // 켜지면 일단 똑바로 위로 솟는다. 소환하는 쪽이 바로 이어서 Launch로 방향을 다시 정해 준다.
    private void OnEnable()
    {
        Launch(Vector2.up);
    }

    // 지금 위치에서 정해 준 방향으로 뿜어져 나가기 시작한다. 방향의 크기는 쓰지 않는다. (속도는 Launch Speed)
    public void Launch(Vector2 direction)
    {
        Vector2 launchDirection = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector2.up;
        _rigidbody.linearVelocity = launchDirection * _launchSpeed;
        _hasLanded = false;
        _isResting = false;
        _isCollected = false;
    }

    private void FixedUpdate()
    {
        if (_isResting || _isCollected)
        {
            return;
        }

        Vector2 velocity = _rigidbody.linearVelocity;

        if (velocity.y < -_maxFallSpeed)
        {
            velocity.y = -_maxFallSpeed;
            _rigidbody.linearVelocity = velocity;
        }
    }

    // 물리 엔진이 튕김을 처리한 뒤에 불린다. 바닥이면 가로 속도를 줄이고, 튕겨 오르는 힘이 약하면 멈춘다.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_isResting || _isCollected || !IsFloorContact(collision))
        {
            return;
        }

        _hasLanded = true;
        Vector2 velocity = _rigidbody.linearVelocity;

        if (velocity.y < _restSpeed)
        {
            Rest();
            return;
        }

        velocity.x *= _floorHorizontalKeep;
        _rigidbody.linearVelocity = velocity;
    }

    // 바닥에 맞닿은 채로 미끄러지는 경우(튕겨 오르지 않고 바닥을 따라 움직이는 경우)에도 멈추게 한다.
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (_isResting || _isCollected || !IsFloorContact(collision))
        {
            return;
        }

        _hasLanded = true;

        if (_rigidbody.linearVelocity.y < _restSpeed)
        {
            Rest();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    // 플레이어와 겹친 채로 처음 바닥에 닿는 경우, 닿는 순간 Enter가 다시 오지 않으므로 겹쳐 있는 동안에도 확인한다.
    private void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other);
    }

    // 닿은 면 중 하나라도 바닥(위를 향한 면)이면 true
    private static bool IsFloorContact(Collision2D collision)
    {
        int contactCount = collision.contactCount;

        for (int i = 0; i < contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > FloorNormalThreshold)
            {
                return true;
            }
        }

        return false;
    }

    // 그 자리에 멈춘다. 중력이 바닥에 눌러 두므로 Dynamic 그대로 둔다. (발밑 지형이 움직이면 함께 떨어진다)
    private void Rest()
    {
        _rigidbody.linearVelocity = Vector2.zero;
        _isResting = true;
    }

    private void TryCollect(Collider2D other)
    {
        if (!CanBeCollected || other.gameObject.layer != _playerHurtboxLayer)
        {
            return;
        }

        // 다른 결석의 창이 열려 있는 동안(일시정지 중)에는 줍지 않고 그대로 둔다. 창이 닫힌 뒤 겹쳐 있으면 OnTriggerStay2D로 이어서 주워진다.
        // 같은 물리 프레임에 결석 두 개가 함께 닿아도, 먼저 처리된 결석이 창을 열어 일시정지하므로 나머지는 여기서 기다린다.
        if (GameManager.HasInstance && GameManager.Instance.IsPaused)
        {
            return;
        }

        if (other.GetComponentInParent<Player>() == null)
        {
            return;
        }

        _isCollected = true;
        EventBus<RewardStoneCollectedEvent>.Publish(new RewardStoneCollectedEvent(_rewardType));
        Destroy(gameObject);
    }
}
