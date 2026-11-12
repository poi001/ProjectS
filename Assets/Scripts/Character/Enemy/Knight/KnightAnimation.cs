using UnityEngine;

// Knight 전용 애니메이션. Knight 컨트롤러의 고유 패턴 상태 이름(해시)을 담당한다.
// 공격(Attack, AttackCombo) 상태의 Speed Multiplier는 AttackSpeed 파라미터에 연결한다. (CharacterAnimation.SetAttackSpeed)
public class KnightAnimation : EnemyAnimation
{
    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    private SpriteRenderer _spriteRenderer;
    private MaterialPropertyBlock _textureProperties;
    private Texture _boundTexture;

    public SpriteRenderer SpriteRenderer => _spriteRenderer;

    public static readonly int Attack = Animator.StringToHash("Attack");
    public static readonly int AttackCombo = Animator.StringToHash("AttackCombo");
    public static readonly int Roll = Animator.StringToHash("Roll");
    // 점프 패턴은 Jump(뛰어오름) → Fall(공중, 반복) → Land(착지) 세 단계다. 벽타기의 점프도 Jump를 쓴다.
    public static readonly int Jump = Animator.StringToHash("Jump");
    public static readonly int Fall = Animator.StringToHash("Fall");
    public static readonly int Land = Animator.StringToHash("Land");
    public static readonly int WallHang = Animator.StringToHash("WallHang");
    // 드롭킥(높게 매달린 뒤 대각선 낙하): 시작 → 낙하 중 반복 → 바닥 충격 후
    public static readonly int DropKickStart = Animator.StringToHash("DropKick_Start");
    public static readonly int DropKickLoop = Animator.StringToHash("DropKick_Loop");
    public static readonly int DropKickEnd = Animator.StringToHash("DropKick_End");
    // 슬라이딩(낮게 매달린 뒤 바닥 미끄러짐): 반복하지 않고 마지막 프레임에서 멈춘 채 미끄러진다. 벽에 부딪히면 Slide_End.
    public static readonly int Slide = Animator.StringToHash("Slide");
    public static readonly int SlideEnd = Animator.StringToHash("Slide_End");
    // 기절. 전용 스프라이트가 없어 컨트롤러의 Stun 상태에 Death 클립(Knight_Dead)을 연결해 둔다.
    public static readonly int Stun = Animator.StringToHash("Stun");
    // 기절 후 일어나기. 컨트롤러의 StunRecover 상태에 기절과 같은 클립(Knight_Dead)을 연결하고 Speed를 -1로 두어 거꾸로 재생한다.
    // 역재생이어도 Play로 처음부터 재생하고 IsFinished로 끝을 확인하면 된다. (진행도 0이 쓰러진 모습, 1이 일어선 모습)
    public static readonly int StunRecover = Animator.StringToHash("StunRecover");

    public override void Initialize()
    {
        base.Initialize();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _textureProperties = new MaterialPropertyBlock();
        _boundTexture = null;
        RefreshShaderTexture();
    }

    // Unity 6에서 All In 1 머티리얼로 교체해도 현재 애니메이션의 스프라이트 텍스처를 사용하도록 한다.
    // Animator가 스프라이트를 갱신한 뒤 확인하며, 시트가 바뀔 때만 재사용하는 프로퍼티 블록을 갱신한다.
    private void LateUpdate()
    {
        RefreshShaderTexture();
    }

    private void RefreshShaderTexture()
    {
        if (_spriteRenderer == null || _spriteRenderer.sprite == null)
        {
            return;
        }

        Texture texture = _spriteRenderer.sprite.texture;

        if (_boundTexture == texture)
        {
            return;
        }

        _spriteRenderer.GetPropertyBlock(_textureProperties);
        _textureProperties.SetTexture(MainTextureId, texture);
        _spriteRenderer.SetPropertyBlock(_textureProperties);
        _boundTexture = texture;
    }
}
