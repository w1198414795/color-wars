﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ball : MonoBehaviour {

    // A player is allowed to "possess" the ball
    public BallCarrier owner = null;

	public void OnCollisionEnter2D(Collision2D collision) {
		var collided = collision.gameObject;
		// The assumption here is that a gameObject will have a BallCarrier component
		// iff the gameObject can own/carry the ball
		var player = collided.GetComponent<BallCarrier>();
		if (player != null) {
			owner = player;
			player.NotifyCarry(this.gameObject);
		}
	}
    
}