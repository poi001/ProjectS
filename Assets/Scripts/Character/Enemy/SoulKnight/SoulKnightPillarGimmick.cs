using System.Collections.Generic;
using UnityEngine;

// SoulKnight 2페이즈 기도 기믹의 기둥들을 관리한다. SoulKnight 루트에 붙인다. (Enemy_GDD 6.11)
// 기도를 시작하면(Begin) 맵의 좌우 벽 사이에 기둥 여러 개를 같은 간격으로 솟아오르게 하고, 그중 하나를 무작위로 골라 꼭대기에 목표물을 놓는다.
// 플레이어가 목표물을 공격하면 IsCleared가 true가 된다. 기도 상태가 이를 보고 기도를 끝낸다.
// 기도가 끝나면(End) 기둥을 모두 바닥 아래로 내려보낸 뒤 풀로 돌려보낸다. 기둥이 나와 있거나 내려가는 동안에는 IsBusy가 true다.
// 기둥 위치는 정해 둔 좌표가 아니라 시작할 때 보스 위치에서 좌우 벽과 바닥을 찾아 계산한다. (맵 크기가 달라져도 같은 규칙으로 놓인다)
// 기둥은 PoolManager로 꺼내 쓰므로 전투 씬에 PoolManager가 있어야 한다.
public class SoulKnightPillarGimmick : MonoBehaviour
{
    private const string WallLayerName = "Wall";
    private const string GroundLayerName = "Ground";
    // 좌우 벽과 바닥을 찾을 때 검사하는 최대 거리
    private const float SearchDistance = 100f;
    // 기둥이 다 내려갔는지 시간으로 판단할 때 내려가는 시간에 더하는 여유 (단위: 초)
    private const float SinkEndMargin = 0.1f;

    [Header("기둥")]
    [Tooltip("솟아오르는 기둥 프리팹입니다. 꼭대기에 목표물이 들어 있어야 합니다.")]
    [SerializeField] private SoulKnightPillar _pillarPrefab;

    [Tooltip("솟아오르는 기둥의 개수입니다. 좌우 벽 사이를 (개수 + 1)등분한 자리에 하나씩 놓여, 벽과 기둥·기둥과 기둥 사이 간격이 모두 같습니다.")]
    [SerializeField, Min(1)] private int _pillarCount = 4;

    [Tooltip("기둥이 바닥 아래에서 끝까지 솟아오르는 데 걸리는 시간입니다. (단위: 초)\n너무 짧으면 기둥 위에 서 있던 플레이어가 튕겨 올라 화면 밖으로 나갑니다. 기둥 높이 6.5 기준 1.1초 이상으로 둡니다.")]
    [SerializeField, Min(0.01f)] private float _riseDuration = 1.2f;

    [Tooltip("기믹이 끝났을 때 기둥이 바닥 아래로 다 내려가는 데 걸리는 시간입니다. (단위: 초)\n기둥이 다 내려갈 때까지 보스는 Ghost 레이어로 남고 다음 패턴을 시작하지 않으므로, Idle 시간보다 짧게 둡니다.")]
    [SerializeField, Min(0.01f)] private float _sinkDuration = 0.4f;

    private readonly List<SoulKnightPillar> _pillars = new List<SoulKnightPillar>();
    private int _wallLayerMask;
    private int _groundLayerMask;
    private float _groundY;
    // 기둥이 솟아 있는지 (Begin ~ End)
    private bool _isRaised;
    // 기둥이 내려가는 중인지와 다 내려가는 시각
    private bool _isSinking;
    private float _sinkEndTime;

    // 이번 기믹에서 목표물이 공격을 맞았는지. Begin에서 false로 돌아간다.
    public bool IsCleared { get; private set; }
    // 기둥이 나와 있거나 내려가는 중인지
    public bool IsBusy => _isRaised || _isSinking;

    private void Awake()
    {
        _wallLayerMask = LayerMask.GetMask(WallLayerName);
        _groundLayerMask = LayerMask.GetMask(GroundLayerName);
    }

    private void Update()
    {
        if (_isSinking && Time.time >= _sinkEndTime)
        {
            _isSinking = false;
            DespawnPillars();
        }
    }

    private void OnDestroy()
    {
        Clear();
    }

    // 기믹을 시작한다. origin(보스 몸의 중심)에서 좌우 벽과 바닥을 찾아 기둥을 솟아오르게 한다.
    // playerBody(플레이어의 몸 콜라이더)는 기둥이 솟는 자리에 서 있는 플레이어가 튕겨 오르지 않도록 기둥이 단단해질 때를 정하는 데 쓴다.
    // 벽이나 바닥을 찾지 못하면 기둥 없이 진행한다. (IsBusy가 false로 남는다)
    public void Begin(Vector2 origin, Collider2D playerBody)
    {
        // 이전 기둥이 아직 남아 있으면 바로 치우고 새로 시작한다.
        Clear();

        if (_pillarPrefab == null)
        {
            return;
        }

        if (!PoolManager.HasInstance)
        {
            Debug.LogError("[SoulKnightPillarGimmick] 씬에 PoolManager가 없어 기둥을 만들 수 없습니다.", this);
            return;
        }

        RaycastHit2D leftHit = Physics2D.Raycast(origin, Vector2.left, SearchDistance, _wallLayerMask);
        RaycastHit2D rightHit = Physics2D.Raycast(origin, Vector2.right, SearchDistance, _wallLayerMask);
        RaycastHit2D groundHit = Physics2D.Raycast(origin, Vector2.down, SearchDistance, _groundLayerMask);

        if (leftHit.collider == null || rightHit.collider == null || groundHit.collider == null)
        {
            Debug.LogWarning("[SoulKnightPillarGimmick] 좌우 벽 또는 바닥을 찾지 못해 기둥을 만들지 않습니다.", this);
            return;
        }

        _groundY = groundHit.point.y;
        float spacing = (rightHit.point.x - leftHit.point.x) / (_pillarCount + 1);
        int targetIndex = Random.Range(0, _pillarCount);

        for (int i = 0; i < _pillarCount; i++)
        {
            float centerX = leftHit.point.x + spacing * (i + 1);
            GameObject instance = PoolManager.Instance.Spawn(_pillarPrefab.gameObject, new Vector3(centerX, _groundY, 0f), Quaternion.identity);

            if (instance == null)
            {
                continue;
            }

            SoulKnightPillar pillar = instance.GetComponent<SoulKnightPillar>();
            pillar.TargetHit += OnTargetHit;
            pillar.Rise(centerX, _groundY, i == targetIndex, _riseDuration, playerBody);
            _pillars.Add(pillar);
        }

        _isRaised = _pillars.Count > 0;
    }

    // 기믹을 끝낸다. 기둥을 모두 바닥 아래로 내려보내고, 다 내려가면 풀로 돌려보낸다. 기둥이 나와 있지 않으면 아무것도 하지 않는다.
    public void End()
    {
        if (!_isRaised)
        {
            return;
        }

        _isRaised = false;
        _isSinking = true;
        _sinkEndTime = Time.time + _sinkDuration + SinkEndMargin;

        for (int i = 0; i < _pillars.Count; i++)
        {
            _pillars[i].Sink(_groundY, _sinkDuration);
        }
    }

    // 기둥을 내려보내는 연출 없이 바로 치운다. (전투 종료·Release 시)
    public void Clear()
    {
        _isRaised = false;
        _isSinking = false;
        IsCleared = false;
        DespawnPillars();
    }

    private void DespawnPillars()
    {
        for (int i = 0; i < _pillars.Count; i++)
        {
            SoulKnightPillar pillar = _pillars[i];

            // 씬 전환 등으로 이미 파괴된 기둥은 건너뛴다.
            if (pillar == null)
            {
                continue;
            }

            pillar.TargetHit -= OnTargetHit;

            if (PoolManager.HasInstance)
            {
                PoolManager.Instance.Despawn(pillar.gameObject);
            }
            else
            {
                Destroy(pillar.gameObject);
            }
        }

        _pillars.Clear();
    }

    private void OnTargetHit()
    {
        IsCleared = true;
    }
}
