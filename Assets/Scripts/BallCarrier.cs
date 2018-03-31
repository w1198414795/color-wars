﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityExtensions;

using IC = InControl;

public class BallCarrier : MonoBehaviour {

    public float coolDownTime = .1f;
    public Ball ball { private set; get;}
    public float ballTurnSpeed = 10f;
    public bool chargedBallStuns = false;
    public bool slowMoOnCarry = true;
    public float aimAssistThreshold = 7.5f;
    public float aimAssistLerpAmount = .5f;
    public float goalAimmAssistOffset = 1f;

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

    const float ballOffsetMultiplier = 1.07f;

    public bool IsCarryingBall() {
        return ball != null;
    }

    void Start() {
        player = GetComponent<Player>();
        playerMovement = GetComponent<IPlayerMovement>();
        stateManager = GetComponent<PlayerStateManager>();
        rb2d = GetComponent<Rigidbody2D>();
        if (playerMovement != null && stateManager != null) {
            stateManager.CallOnStateEnter(
                State.Posession, playerMovement.FreezePlayer);
            stateManager.CallOnStateExit(
                State.Posession, playerMovement.UnFreezePlayer);
        }
        laserGuide = this.GetComponent<LaserGuide>();
    }

    // This function is called when the BallCarrier initially gains possession
    // of the ball
    public void StartCarryingBall(Ball ball) {
        CalculateOffset(ball);
        if (slowMoOnCarry) {
            GameModel.instance.SlowMo();
        }
        ball.charged = false;
        Utility.TutEvent("BallPickup", this);
        laserGuide?.DrawLaser();
        var player = GetComponent<Player>();
        var lastPlayer = ball.lastOwner?.GetComponent<Player>();
        if (player != null && lastPlayer != null) {
            if (player.team == lastPlayer.team && player != lastPlayer) {
                Utility.TutEvent("PassSwitch", player);
                Utility.TutEvent("PassSwitch", lastPlayer);
            }
        }
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

    GameObject GetTeammate() {
        if (teammate != null) {
            return teammate;
        }
        var team = player.team;
        if (team == null) {
            return null;
        }
        foreach (var teammate in team.teamMembers) {
            if (teammate != player) {
                return teammate.gameObject;
            }
        }
        return null;
    }

    GameObject GetGoal() {
        if (goal != null) {
            return goal;
        }
        return GameObject.FindObjectOfType<Goal>().gameObject;
    }

    void RotateTowards(Vector2 towards) {
        var lerpedVector = Vector2.Lerp(transform.right, towards, aimAssistLerpAmount);
        rb2d.rotation = Vector2.SignedAngle(Vector2.right, lerpedVector);
    }

    void SnapAimTowardsTargets() {
        playerMovement?.RotatePlayer();
        Vector2 targetVector = Vector2.zero;
        var goalVector = ((GetGoal().transform.position + Vector3.up) - transform.position).normalized;
        var teammateVector = (GetTeammate().transform.position - transform.position).normalized;
        if (Mathf.Abs(Vector2.Angle(transform.right, goalVector)) < aimAssistThreshold) {
            RotateTowards(goalVector);
        } else if (Mathf.Abs(Vector2.Angle(transform.right, teammateVector)) < aimAssistThreshold) {
            RotateTowards(teammateVector);
        }
    }

    IEnumerator CarryBall(Ball ball) {
        GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        this.ball = ball;
        ball.owner = this;

        while (true) {
            SnapAimTowardsTargets();
            PlaceBallAtNose();
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

            // Reset references
            ball.owner = null;
            ball = null;
            laserGuide?.StopDrawingLaser();
            StartCoroutine(CoolDownTimer());
        }
    }

    Vector2 NosePosition(Ball ball) {
        var newPosition = transform.position + transform.right * ballOffsetFromCenter;
        return newPosition;
    }

    void PlaceBallAtNose() {
        if (ball != null) {
            var rigidbody = ball.GetComponent<Rigidbody2D>();
            Vector2 newPosition = CircularLerp(
                ball.transform.position, NosePosition(ball), transform.position,
                ballOffsetFromCenter, Time.deltaTime, ballTurnSpeed);
            rigidbody.MovePosition(newPosition);
        }
    }

    Vector2 CircularLerp(Vector2 start, Vector2 end, Vector2 center, float radius,
                      float timeDelta, float speed) {
        float angularDistance = timeDelta * speed;
        var centeredStart = start - center;
        var centerToStartDirection = centeredStart.normalized;

        var centeredEndDirection = (end - center).normalized;
        var angle = Vector2.SignedAngle(centerToStartDirection, centeredEndDirection);
        var arcDistance = radius * 2 * Mathf.PI * Mathf.Abs(angle / 360);
        var percentArc = Mathf.Clamp(angularDistance / arcDistance, 0, 1);

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
            if (chargedBallStuns && ball.charged && last_team != this_team) {
                var stun = GetComponent<PlayerStun>();
                var direction = transform.position - ball.transform.position;
                var knockback = ball.GetComponent<Rigidbody2D>().velocity.magnitude * direction;
                stateManager.AttemptStun(() => stun.StartStun(knockback), stun.StopStunned);
            } else {
                stateManager.AttemptPossession(() => StartCarryingBall(ball), DropBall);
            }
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
}
