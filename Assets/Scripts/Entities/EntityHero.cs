﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityHero : M8.EntityBase {
    public const float radiusCheckOfs = 0.2f;

    public enum MoveState {
        Stop,
        Left,
        Right
    }

    [Header("Data")]
    public float jumpImpulse = 5f;
    public float jumpCancelImpulse = 5f;

    [Header("AI Data")]
    public LayerMask solidCheckMask;
    public LayerMask harmCheckMask;

    public float groundCheckForwardDist = 0.25f;
    public float groundCheckLastJumpDelay = 2f;

    public EntityHeroMoveController moveCtrl { get { return mMoveCtrl; } }

    public MoveState moveState {
        get { return mMoveState; }
        set {
            if(mMoveState != value) {
                if(mMoveState != MoveState.Stop)
                    mMoveStatePrev = mMoveState;

                mMoveState = value;

                switch(mMoveState) {
                    case MoveState.Stop:
                        mMoveCtrl.moveHorizontal = 0f;
                        break;
                    case MoveState.Left:
                        mMoveCtrl.moveHorizontal = -1f;
                        break;
                    case MoveState.Right:
                        mMoveCtrl.moveHorizontal = 1f;
                        break;
                }
            }
        }
    }

    public MoveState moveStatePrev { get { return mMoveStatePrev; } }

    public bool isJumping { get { return mJumpRout != null; } }
    
    /// <summary>
    /// Elapsed time since we finished jumping
    /// </summary>
    public float jumpFinishElapsed { get { return Time.fixedTime - mJumpEndLastTime; } }

    private EntityHeroMoveController mMoveCtrl;

    private MoveState mMoveState;
    private MoveState mMoveStatePrev;

    private float mJumpEndLastTime;

    private Coroutine mJumpRout;

    public void Jump() {
        if(mJumpRout == null)
            mJumpRout = StartCoroutine(DoJump());
    }

    public void JumpCancel() {
        if(mJumpRout != null) {
            mJumpEndLastTime = Time.fixedTime;

            var lvel = mMoveCtrl.localVelocity;
            if(lvel.y > 0f)
                mMoveCtrl.body.AddForce(Vector2.down * jumpCancelImpulse, ForceMode2D.Impulse);

            StopCoroutine(mJumpRout);
            mJumpRout = null;
        }
    }

    protected override void OnDespawned() {
        if(mJumpRout != null) {
            StopCoroutine(mJumpRout);
            mJumpRout = null;
        }

        //reset stuff here
        mMoveCtrl.ResetCollision();
    }

    protected override void OnSpawned(M8.GenericParams parms) {
        //initial states
        mMoveStatePrev = MoveState.Right; //TODO: could be set by the map
        mMoveState = MoveState.Stop;

        mJumpEndLastTime = 0f;

        //populate data/state for ai, player control, etc.

        //start ai, player control, etc        
    }

    protected override void OnDestroy() {
        //dealloc here
        if(mMoveCtrl) {
            mMoveCtrl.collisionEnterCallback -= OnMoveCollisionEnter;
            mMoveCtrl.triggerEnterCallback -= OnMoveTriggerEnter;
            mMoveCtrl.triggerExitCallback -= OnMoveTriggerExit;
        }

        base.OnDestroy();
    }

    protected override void Awake() {
        base.Awake();

        //initialize data/variables
        mMoveCtrl = GetComponentInChildren<EntityHeroMoveController>();

        mMoveCtrl.collisionEnterCallback += OnMoveCollisionEnter;
        mMoveCtrl.triggerEnterCallback += OnMoveTriggerEnter;
        mMoveCtrl.triggerExitCallback += OnMoveTriggerExit;
    }

    // Use this for one-time initialization
    protected override void Start() {
        base.Start();

        //initialize variables from other sources (for communicating with managers, etc.)
    }

    void FixedUpdate() {
        //ground
        if(mMoveCtrl.isGrounded)
            GroundUpdate();
        else if(mMoveCtrl.isSlopSlide) {
            //TODO
        }
        else
            AirUpdate();
    }

    void GroundUpdate() {
        if(moveState == MoveState.Stop)
            return;

        if(isJumping)
            return;

        bool moveOpposite = false;

        //check forward to see if there's an obstacle
        Vector2 up = mMoveCtrl.dirHolder.up;
        Vector2 dir = new Vector2(Mathf.Sign(mMoveCtrl.moveHorizontal), 0f);

        RaycastHit2D hit;
        if(mMoveCtrl.CheckCast(radiusCheckOfs, dir, out hit, mMoveCtrl.radius + groundCheckForwardDist, solidCheckMask | harmCheckMask)) {
            //check if it's harm's way
            if(((1 << hit.transform.gameObject.layer) & harmCheckMask) != 0) {
                //move the opposite direction
                moveOpposite = true;
            }
            else {
                //check if it's a wall
                var collFlag = mMoveCtrl.GetCollisionFlag(up, hit.normal);
                if(collFlag == CollisionFlags.Sides) {
                    //only jump if we haven't jumped in a while
                    if(jumpFinishElapsed >= groundCheckLastJumpDelay) {
                        //jump!
                        Jump();
                    }
                    else
                        moveOpposite = true;
                }
            }

            if(moveOpposite) {
                switch(moveState) {
                    case MoveState.Left:
                        moveState = MoveState.Right;
                        break;
                    case MoveState.Right:
                        moveState = MoveState.Left;
                        break;
                }
            }
        }
    }

    void AirUpdate() {

    }

    IEnumerator DoJump() {
        var wait = new WaitForFixedUpdate();

        Vector2 lastPos = transform.position;

        mMoveCtrl.body.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);

        //mMoveCtrl.moveVertical = jumpMagnitude;

        while(true) {
            yield return wait;

            //hit a ceiling?
            if((mMoveCtrl.collisionFlags & CollisionFlags.Above) != CollisionFlags.None)
                break;
            
            //check if velocity is downward
            if(mMoveCtrl.localVelocity.y < 0f)
                break;
        }

        mJumpEndLastTime = Time.fixedTime;
        
        mJumpRout = null;
    }

    void OnMoveCollisionEnter(EntityHeroMoveController ctrl, Collision2D coll) {
        //check if it's a side collision
        //if(!ctrl.isSlopSlide && (ctrl.collisionFlags & CollisionFlags.Sides) != CollisionFlags.None)
            //ctrl.moveHorizontal *= -1.0f;
    }

    void OnMoveTriggerEnter(EntityHeroMoveController ctrl, Collider2D coll) {

    }

    void OnMoveTriggerExit(EntityHeroMoveController ctrl, Collider2D coll) {

    }
}
