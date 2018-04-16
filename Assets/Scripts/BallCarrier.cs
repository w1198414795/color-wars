﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityExtensions;
using System.Linq;

using IC = InControl;

public class BallCarrier : MonoBehaviour {

    public GameObject blowbackEffectPrefab;
    public float coolDownTime = .1f;
    public Ball ball { private set; get;}
    float ballTurnSpeed = 10f;
    public bool slowMoOnCarry = true;
    public float aimAssistThreshold = 20f;
    public float aimAssistLerpAmount = .5f;
    public float goalAimAssistOffset = 1f;
    public float delayBetweenSnaps = .2f;
    public float snapEpsilon = 5f;
    public float snapLerpStrength = .5f;
    public float timeCarryStarted {get; private set;}
    public float blowbackRadius = 3f;
    public float blowbackForce = 5f;
    public float blowbackStunTime = 0.1f;

    float ballOffsetFromCenter = .5f;
    IPlayerMovement playerMovement;
    PlayerStateManager stateManager;
    Coroutine carryBallCoroutine;
    bool isCoolingDown = false;
    LaserGuide laserGuide;
    GameObject teammate;
    Player player;
    GameObject goal;
    Rigidbody2D rb2d;
    GameObject snapToObject;
    float snapDelay = 0f;
    Vector2 stickAngleWhenSnapped;

    const float ballOffsetMultiplier = 1.07f;

    public bool IsCarryingBall() {
        return ball != null;
    }

    void Start() {
        snapToObject = null;
        player = GetComponent<Player>();
        playerMovement = GetComponent<IPlayerMovement>();
        stateManager = GetComponent<PlayerStateManager>();
        rb2d = GetComponent<Rigidbody2D>();
        if (playerMovement != null && stateManager != null) {
            stateManager.CallOnStateEnter(
                State.Posession, playerMovement.FreezePlayer);
            stateManager.CallOnStateExit(
                State.Posession, playerMovement.UnFreezePlayer);
            if (playerMovement.GetType() == typeof(PlayerMovement)) {
                ballTurnSpeed = (playerMovement as PlayerMovement).rotationSpeed;
            }
        }
        laserGuide = this.GetComponent<LaserGuide>();
        this.FrameDelayCall(() => {GetGoal(); GetTeammate();}, 2);
    }

    void BlowBackEnemyPlayers() {
        if (player.team == null) {
            return;
        }
        var enemyTeam = GameModel.instance.teams.Find((teamManager) => teamManager != player.team);
        Debug.Assert(enemyTeam != null);

        {
            // Because C# doesn't have lvalue references. FML.
            var effect = Instantiate(blowbackEffectPrefab, transform.position, transform.rotation);
            var ps     = effect.GetComponent<ParticleSystem>();
            var col    = ps.colorOverLifetime;

            col.enabled = true;

            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(player.team.teamColor, 0.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f,  0.0f),
                    new GradientAlphaKey(0.25f, 0.75f),
                    new GradientAlphaKey(0.0f,  1.0f)
                }
            );
            col.color = grad;

            Destroy(effect, 1.0f);
        }

        foreach (var enemyPlayer in enemyTeam.teamMembers) {
            var blowBackVector = enemyPlayer.transform.position - transform.position;
            if (blowBackVector.magnitude < blowbackRadius) {
                var otherStun = enemyPlayer.GetComponent<PlayerStun>();
                var otherStateManager = enemyPlayer.GetComponent<PlayerStateManager>();
                if (otherStun != null && otherStateManager != null) {
                    otherStateManager.AttemptStun(() => otherStun.StartStun(blowBackVector.normalized * blowbackForce, blowbackStunTime), otherStun.StopStunned);
                }
            }
        }
    }

    // This function is called when the BallCarrier initially gains possession
    // of the ball
    public void StartCarryingBall(Ball ball) {
        BlowBackEnemyPlayers();
        timeCarryStarted = Time.time;
        ball.rigidbody.velocity = Vector2.zero;
        ball.rigidbody.angularVelocity = 0;
        CalculateOffset(ball);
        if (slowMoOnCarry) {
            GameModel.instance.SlowMo();
        }
        laserGuide?.DrawLaser();
        carryBallCoroutine = StartCoroutine(CarryBall(ball));
    }

    void CalculateOffset(Ball ball) {
        var ballRadius = ball.GetComponent<CircleCollider2D>()?.bounds.extents.x;
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null && ballRadius != null) {
            var spriteExtents = renderer.sprite.bounds.extents.x * transform.localScale.x;
            ballOffsetFromCenter = ballOffsetMultiplier * (spriteExtents + ballRadius.Value);
        }
    }

    void GetTeammate() {
        var team = player.team;
        if (team == null) {
            return;
        }
        foreach (var teammate in team.teamMembers) {
            if (teammate != player) {
                this.teammate = teammate.gameObject;
            }
        }
    }

    void GetGoal() {
        goal = GameObject.FindObjectOfType<GoalAimPoint>()?.gameObject;
    }

    void SnapToGameObject() {
        var vector = (snapToObject.transform.position - transform.position).normalized;
        rb2d.rotation = Vector2.SignedAngle(Vector2.right, Vector2.Lerp(transform.right, vector, snapLerpStrength));
    }

    void SnapAimTowardsTargets() {
        if (playerMovement == null
            || player == null) {
            return;
        }
        if (snapDelay > 0f) {// || TutorialLiveClips.runningLiveClips || PlayerRecorder.isRecording) {
            playerMovement?.RotatePlayer();
            return;
        }
        PlayerMovement pm = (PlayerMovement)playerMovement;
        var stickDirection = pm.lastDirection;
        if (snapToObject != null) {
            var vector = (snapToObject.transform.position - transform.position).normalized;
            if (stickDirection == Vector2.zero ||
                Mathf.Abs(Vector2.Angle(vector, stickDirection)) < aimAssistThreshold ||
                Mathf.Abs(Vector2.Angle(stickAngleWhenSnapped, stickDirection)) < snapEpsilon) {
                SnapToGameObject();
            } else {
                snapDelay = delayBetweenSnaps;
                snapToObject = null;
                playerMovement?.RotatePlayer();
            }
        } else {
            if (stickDirection == Vector2.zero) {
                playerMovement?.RotatePlayer();
                return;
            }

            Vector2? goalVector;
            Vector2? teammateVector;
            if (goal != null) {
                goalVector = ((goal.transform.position + Vector3.up) - transform.position).normalized;
            }
            if (teammate != null) {
                teammateVector = (teammate.transform.position - transform.position).normalized;
            }

            if (goalVector.HasValue &&
                    Mathf.Abs(Vector2.Angle(transform.right, goalVector.Value)) < aimAssistThreshold &&
                ball.renderer.color == player.team.teamColor.color) {
                snapToObject = goal;
                stickAngleWhenSnapped = stickDirection;
                SnapToGameObject();
            } else if (teammateVector.HasValue &&
                           Mathf.Abs(Vector2.Angle(transform.right, teammateVector.Value)) < aimAssistThreshold) {
                snapToObject = teammate;
                stickAngleWhenSnapped = stickDirection;
                SnapToGameObject();
            } else {
                playerMovement?.RotatePlayer();
            }
        }
    }

    IEnumerator CarryBall(Ball ball) {
        GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        this.ball = ball;
        ball.owner = this;
        snapToObject = null;

        while (true) {
            SnapAimTowardsTargets();
            PlaceBallAtNose();
            snapDelay -= Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    IEnumerator CoolDownTimer() {
        isCoolingDown = true;
        yield return new WaitForSeconds(coolDownTime);
        isCoolingDown = false;
    }

    public void DropBall() {
        if (ball != null) {
            GameModel.instance.ResetSlowMo();
            StopCoroutine(carryBallCoroutine);
            carryBallCoroutine = null;
            snapToObject = null;

            // Reset references
            ball.owner = null;
            ball = null;

            laserGuide?.StopDrawingLaser();
            if (this.isActiveAndEnabled) {
                StartCoroutine(CoolDownTimer());
            }
        }
    }

    Vector2 NosePosition(Ball ball) {
        var newPosition = transform.position + transform.right * ballOffsetFromCenter;
        return newPosition;
    }

    void PlaceBallAtNose() {
        if (ball != null) {
            var rigidbody = ball.GetComponent<Rigidbody2D>();
            Vector2 newPosition =
                CircularLerp(ball.transform.position, NosePosition(ball), transform.position,
                             ballOffsetFromCenter, Time.deltaTime, ballTurnSpeed);
            rigidbody.MovePosition(newPosition);
        }
    }

    Vector2 CircularLerp(Vector2 start, Vector2 end, Vector2 center, float radius,
                         float timeDelta, float speed) {
        float angleMax = timeDelta * speed;
        var centeredStart = start - center;
        var centerToStartDirection = centeredStart.normalized;

        var centeredEndDirection = (end - center).normalized;
        var angle = Vector2.SignedAngle(centerToStartDirection, centeredEndDirection);
        var percentArc = Mathf.Clamp(angleMax / Mathf.Abs(angle / 360), 0, 1);

        var rotation = Quaternion.AngleAxis(angle * percentArc, Vector3.forward);
        var centeredResult = rotation * centerToStartDirection;
        centeredResult *= radius;
        return (Vector2) centeredResult + center;
    }

    void HandleCollision(GameObject thing) {
        var ball = thing.GetComponent<Ball>();
        if (ball == null || !ball.IsOwnable() || isCoolingDown) {
            return;
        }
        if (stateManager != null) {
            var last_team = ball.lastOwner?.GetComponent<Player>().team;
            var this_team = GetComponent<Player>().team;
            stateManager.AttemptPossession(() => StartCarryingBall(ball), DropBall);
        } else {
            StartCoroutine(CoroutineUtility.RunThenCallback(CarryBall(ball), DropBall));
        }
    }

    public void OnCollisionEnter2D(Collision2D collision) {
        HandleCollision(collision.gameObject);
    }

    public void OnCollisionStay2D(Collision2D collision) {
        HandleCollision(collision.gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (stateManager != null && stateManager.IsInState(State.Dash)) {
            HandleCollision(other.gameObject);
        }
    }

    void OnDestroy() {
        DropBall();
    }
}
