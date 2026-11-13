using UnityEngine;

public enum SoulKnightMotion
{
    Attack1, Attack2, Attack3, Attack4, Jump, JumpAttack, Sliding, Praying, Health, Roll, CrouchIdle, Death
}

// SoulKnight 전용 애니메이션. SoulKnight 컨트롤러의 고유 패턴 상태 이름(해시)을 담당한다.
public sealed class SoulKnightAnimation : EnemyAnimation
{
    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    private SpriteRenderer _spriteRenderer;
    private MaterialPropertyBlock _textureProperties;
    private Texture _boundTexture;

    public SpriteRenderer SpriteRenderer => _spriteRenderer;

    public static readonly int Roll = Animator.StringToHash("Roll");
    // 슬라이딩은 클립 하나를 진행도로 나눠 쓴다. 앞부분(눕는 모습)에서 멈춘 채 미끄러지고, 뒷부분(일어나기)은 멈춘 뒤에 재생한다.
    public static readonly int Sliding = Animator.StringToHash("Sliding");
    public static readonly int Jump = Animator.StringToHash("Jump");
    // 점프공격도 클립 하나를 진행도로 나눠 쓴다. 앞부분(낙하)에서 멈춘 채 떨어지고, 뒷부분(착지 충격)은 바닥에 닿은 뒤에 재생한다.
    public static readonly int JumpAttack = Animator.StringToHash("JumpAttack");
    // 기절. 전용 스프라이트가 없어 컨트롤러의 Stun 상태에 Death 클립(SoulKnight_Death)을 연결해 둔다.
    public static readonly int Stun = Animator.StringToHash("Stun");
    // 기절 후 일어나기. 컨트롤러의 StunRecover 상태에 기절과 같은 클립을 연결하고 Speed를 -1로 두어 거꾸로 재생한다.
    // 역재생이어도 Play로 처음부터 재생하고 IsFinished로 끝을 확인하면 된다. (진행도 0이 쓰러진 모습, 1이 일어선 모습)
    public static readonly int StunRecover = Animator.StringToHash("StunRecover");
    // 공격 4타. 컨트롤러 상태 이름은 Attack1~4이며, 3타는 찌르기(Thrust), 4타는 내려치기(Smash) 상태가 재생한다.
    // Attack1·Attack2·Attack4 클립에는 칼이 맞는 프레임에 OnAttackHitFrame 애니메이션 이벤트를 넣는다. Attack3(찌르기)은 이벤트 없이 판정한다.
    // 공격 전 딜레이. crouch_idle_0 한 장을 0.3초 보여 주는 반복 없는 클립(SoulKnight_AttackDelay)이며, 클립 길이가 딜레이 시간이다.
    public static readonly int AttackDelay = Animator.StringToHash("AttackDelay");
    public static readonly int Attack1 = Animator.StringToHash("Attack1");
    public static readonly int Attack2 = Animator.StringToHash("Attack2");
    public static readonly int Thrust = Animator.StringToHash("Attack3");
    public static readonly int Smash = Animator.StringToHash("Attack4");
    // 1페이즈 다운. 1페이즈의 체력을 모두 잃었을 때 쓰러져 있는 모습이다. crouch_idle 시트를 반복하는 클립(SoulKnight_Phase1Down)이며,
    // 쓰러져 있는 시간은 클립이 아니라 SoulKnight의 Phase 1 Down Duration이 정한다.
    public static readonly int Phase1Down = Animator.StringToHash("Phase1Down");
    // 기도. 반복하지 않는 5초 클립이며, 이 클립이 끝나는 시점이 기도가 끝나는 시점이다. (SoulKnightPrayingState)
    public static readonly int Praying = Animator.StringToHash("Praying");

    public override void Initialize()
    {
        base.Initialize();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _textureProperties = new MaterialPropertyBlock();
        _boundTexture = null;
        RefreshShaderTexture();
    }

    public static int GetMotionHash(SoulKnightMotion motion)
    {
        if (motion == SoulKnightMotion.Death)
        {
            return Dead;
        }

        return Animator.StringToHash(motion.ToString());
    }

    // 기절 가능 표시로 All In 1 머티리얼로 바꿔도 현재 애니메이션의 스프라이트 텍스처를 쓰도록 한다. (KnightAnimation과 같은 방식)
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
