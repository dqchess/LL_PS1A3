﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityHero : M8.EntityBase {
    public const int heatCollideCapacity = 8;
    public const float radiusCheckOfs = 0.2f;

    public enum MoveState {
        Stop,
        Left,
        Right
    }

    [Header("Components")]
    public GameObject touchGO;

    [Header("Data")]
    public MoveState moveStart = MoveState.Right;
    public float jumpImpulse = 5f;
    public float jumpCancelImpulse = 5f;
    public float heatThreshold = 5f;

    [Header("AI Data")]
    public LayerMask solidCheckMask;
    public LayerMask harmCheckMask;

    public float groundCheckForwardDist = 0.3f; //add radius
    public float groundCheckLastJumpDelay = 2f;
    public float groundCheckDownDist = 3.1f; //add radius

    public EntityHeroMoveController moveCtrl { get { return mMoveCtrl; } }

    public MoveState moveState {
        get { return mMoveState; }
        set {
            if(mMoveState != value) {
                if(mMoveState != MoveState.Stop)
                    mMoveStatePrev = mMoveState;

                mMoveState = value;

                RefreshMoveState();
            }
        }
    }

    public MoveState moveStatePrev { get { return mMoveStatePrev; } }

    public bool isJumping { get { return mJumpRout != null; } }
    
    /// <summary>
    /// time since we finished jumping
    /// </summary>
    public float lastJumpTime { get { return mJumpEndLastTime; } }

    private EntityHeroMoveController mMoveCtrl;

    private MoveState mMoveState;
    private MoveState mMoveStatePrev;

    private float mJumpEndLastTime;

    private Collider2D mGroundLastWallChecked;

    private Vector2 mStartPos;

    private Coroutine mJumpRout;
    private Coroutine mStateRout;

    private M8.CacheList<HeatController.Contact> mHeatCollides;

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

    protected override void StateChanged() {
        JumpCancel();

        ClearStateRout();

        bool touchActive = false;
        bool physicsActive = false;
        MoveState toMoveState = MoveState.Stop;

        switch((EntityState)state) {
            case EntityState.Spawn:
                mStateRout = StartCoroutine(DoSpawn());
                break;

            case EntityState.Normal:
                mMoveStatePrev = moveStart;
                toMoveState = moveStart;

                touchActive = true;
                physicsActive = true;                
                break;

            case EntityState.Dead:
                mStateRout = StartCoroutine(DoDead());
                break;

            case EntityState.Victory:
                mStateRout = StartCoroutine(DoVictory());
                break;
        }

        moveState = toMoveState;

        touchGO.SetActive(touchActive);

        if(physicsActive) {
            mMoveCtrl.coll.enabled = true;
            mMoveCtrl.body.simulated = true;
        }
        else {
            mMoveCtrl.ResetCollision();
            mMoveCtrl.coll.enabled = false;
            mMoveCtrl.body.simulated = false;

            mHeatCollides.Clear();
        }
    }

    protected override void OnDespawned() {
        if(mJumpRout != null) {
            StopCoroutine(mJumpRout);
            mJumpRout = null;
        }

        ClearStateRout();

        mGroundLastWallChecked = null;
        
        //reset stuff here
        mMoveCtrl.ResetCollision();

        mHeatCollides.Clear();
    }

    protected override void OnSpawned(M8.GenericParams parms) {
        //initial states
        mMoveStatePrev = moveStart;

        mJumpEndLastTime = 0f;

        //populate data/state for ai, player control, etc.

        //start ai, player control, etc
        state = (int)EntityState.Spawn;
    }

    protected override void OnDestroy() {
        //dealloc here
        if(mMoveCtrl) {
            mMoveCtrl.collisionEnterCallback -= OnMoveCollisionEnter;
            mMoveCtrl.collisionExitCallback -= OnMoveCollisionExit;
            mMoveCtrl.triggerEnterCallback -= OnMoveTriggerEnter;
            mMoveCtrl.triggerExitCallback -= OnMoveTriggerExit;
        }

        base.OnDestroy();
    }

    protected override void Awake() {
        base.Awake();

        //initialize data/variables
        mHeatCollides = new M8.CacheList<HeatController.Contact>(heatCollideCapacity);

        mMoveCtrl = GetComponentInChildren<EntityHeroMoveController>();

        mMoveCtrl.collisionEnterCallback += OnMoveCollisionEnter;
        mMoveCtrl.collisionExitCallback += OnMoveCollisionExit;
        mMoveCtrl.triggerEnterCallback += OnMoveTriggerEnter;
        mMoveCtrl.triggerExitCallback += OnMoveTriggerExit;

        mStartPos = transform.position;
    }

    // Use this for one-time initialization
    protected override void Start() {
        base.Start();

        //initialize variables from other sources (for communicating with managers, etc.)
    }

    void FixedUpdate() {
        //AI during Normal
        if(state == (int)EntityState.Normal) {
            //not moving forward?
            if(mMoveCtrl.moveHorizontal == 0f) {
                //landed, check if we need to resume moving according to move state
                if(mMoveCtrl.isGrounded)
                    RefreshMoveState();
            }
            else if(mMoveCtrl.isSlopSlide) {
                //TODO
            }
            //ground
            else if(mMoveCtrl.isGrounded)
                GroundUpdate();

            //check contacted heat controls if one of them has reached the threshold
            for(int i = 0; i < mHeatCollides.Count; i++) {
                if(mHeatCollides[i].heat.amountCurrent > heatThreshold) {
                    //die
                    state = (int)EntityState.Dead;
                    break;
                }
            }
        }
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
        float forwardDist = mMoveCtrl.radius + groundCheckForwardDist;

        RaycastHit2D hit;
        if(mMoveCtrl.CheckCast(radiusCheckOfs, dir, out hit, forwardDist, solidCheckMask | harmCheckMask)) {
            //check if it's harm's way
            if(((1 << hit.transform.gameObject.layer) & harmCheckMask) != 0) {
                //move the opposite direction
                moveOpposite = true;
            }
            else {
                //check if it's a wall
                var collFlag = mMoveCtrl.GetCollisionFlag(up, hit.normal);
                if(collFlag == CollisionFlags.Sides) {
                    //only jump if it's a new collision
                    if(mGroundLastWallChecked != hit.collider) {
                        mGroundLastWallChecked = hit.collider;

                        //jump!
                        Jump();
                    }
                    else
                        moveOpposite = true;
                }
                else if(collFlag == CollisionFlags.Above) //possibly a roof slanted towards the ground
                    moveOpposite = true;
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
        else {
            //check below
            Vector2 pos = transform.position;
            Vector2 down = -up;
            pos += dir * forwardDist;

            var hitDown = Physics2D.Raycast(pos, down, mMoveCtrl.radius + groundCheckDownDist, solidCheckMask | harmCheckMask);
            if(hitDown.collider) {
                //check if it's harm's way
                if(((1 << hitDown.collider.gameObject.layer) & harmCheckMask) != 0) {
                    //try jumping, that's a good trick
                    Jump();
                }
            }
            else {
                //nothing hit, jump!
                Jump();
            }
        }
    }
    
    IEnumerator DoJump() {
        var wait = new WaitForFixedUpdate();

        Vector2 lastPos = transform.position;

        mMoveCtrl.body.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);

        //mMoveCtrl.moveVertical = jumpMagnitude;

        yield return new WaitForSeconds(0.1f);

        while(true) {
            yield return wait;

            //hit a ceiling?
            if((mMoveCtrl.collisionFlags & CollisionFlags.Above) != CollisionFlags.None)
                break;
            
            //check if velocity is downward
            if(mMoveCtrl.isGrounded)
                break;
        }

        mJumpEndLastTime = Time.fixedTime;
        
        mJumpRout = null;
    }

    IEnumerator DoSpawn() {
        do {
            yield return null;
        } while(M8.SceneManager.instance.isLoading);

        //animations and stuff

        state = (int)EntityState.Normal;
    }

    IEnumerator DoDead() {
        //animations and stuff

        yield return null;

        //return to start position
        transform.position = mStartPos;

        state = (int)EntityState.Normal;
    }

    IEnumerator DoVictory() {
        //animations and stuff

        yield return null;
    }

    void OnMoveCollisionEnter(EntityHeroMoveController ctrl, Collision2D coll) {
        //check if it's a side collision
        //if(!ctrl.isSlopSlide && (ctrl.collisionFlags & CollisionFlags.Sides) != CollisionFlags.None)
        //ctrl.moveHorizontal *= -1.0f;

        //check for heat controller
        var heatCtrl = coll.collider.GetComponent<HeatController>();
        if(heatCtrl) {
            if(mHeatCollides.IsFull) mHeatCollides.Expand();

            mHeatCollides.Add(new HeatController.Contact(coll.collider, heatCtrl));
        }
    }

    void OnMoveCollisionExit(EntityHeroMoveController ctrl, Collision2D coll) {
        //check for heat controller
        var heatCtrl = coll.collider.GetComponent<HeatController>();
        if(heatCtrl) {
            for(int i = 0; i < mHeatCollides.Count; i++) {
                if(mHeatCollides[i].heat == heatCtrl) {
                    mHeatCollides.RemoveAt(i);
                    break;
                }
            }
        }
    }

    void OnMoveTriggerEnter(EntityHeroMoveController ctrl, Collider2D coll) {

    }

    void OnMoveTriggerExit(EntityHeroMoveController ctrl, Collider2D coll) {

    }

    private void RefreshMoveState() {
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

    private void ClearStateRout() {
        if(mStateRout != null) {
            StopCoroutine(mStateRout);
            mStateRout = null;
        }
    }
}
