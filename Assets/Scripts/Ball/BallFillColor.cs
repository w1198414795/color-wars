﻿using UnityEngine;
using UtilityExtensions;

public class BallFillColor : MonoBehaviour
{
    private new SpriteRenderer renderer;

    // Use this for initialization
    private void Start()
    {
        renderer = this.EnsureComponent<SpriteRenderer>();
    }

    public void EnableAndSetColor(Color to_)
    {
        renderer.enabled = true;
        renderer.color = to_;
    }

    public void DisableFill()
    {
        renderer.enabled = false;
    }
}
