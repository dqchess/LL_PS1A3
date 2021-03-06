﻿using System;
using System.Collections;
using System.Collections.Generic;
using M8;
using UnityEngine;

public class BlockMatter : Block {
    public const float overlapThreshold = 0.1f;

    [Header("Matter")]

    public SpriteRenderer spriteBody;
    public float spriteBodyBorderPixelOfs = 3f;

    public SpriteRenderer spriteFrame;

    [Range(0f, 1f)]
    public float ghostAlphaMin = 0.7f;
    [Range(0f, 1f)]
    public float ghostAlphaMax = 0.9f;

    public Color invalidColor = Color.red;

    public override Type type { get { return Type.Matter; } }

    public override CellIndex cellSize { get { return mCellSize; } }

    public override Rigidbody2D mainBody { get { return mBody; } }

    public override Transform editAttach { get { return transform; } }
    
    public override Bounds editBounds {
        get {
            var center = mColl.transform.position;
            var size = mColl.size;

            return new Bounds(center, size);
        }
    }

    public float ghostAlpha {
        get {
            if(!GameMapController.isInstantiated)
                return ghostAlphaMin;

            return Mathf.Lerp(ghostAlphaMin, ghostAlphaMax, GameMapController.instance.curGhostPulseT);
        }
    }
    
    private Rigidbody2D mBody;
    private BoxCollider2D mColl;

    private Color mSpriteBodyDefaultColor;
    private Color mSpriteFrameDefaultColor;

    private CellIndex mCellSize;
    
    private bool mEditIsValid;

    private float mSpriteBodyBorderOfs;

    public override bool EditIsExpandable() {
        return true;
    }
    
    public override void EditSetPosition(Vector2 pos) {
        transform.position = pos;

        UpdatePlacementValid();
        DimensionChanged();
    }

    public override void EditMove(Vector2 delta) {
        var pos = (Vector2)transform.position;
        pos += delta;
        transform.position = pos;

        UpdatePlacementValid();
        DimensionChanged();
    }

    public override void EditExpand(int top, int bottom, int left, int right) {
        var mapData = GameMapController.instance.mapData;

        var cellSize = GameData.instance.blockSize;
        var cellHalfSize = cellSize * 0.5f;

        //grab min and max cell position
        var extents = mColl.size * 0.5f;
        var center = (Vector2)transform.position;

        Vector2 min = (center - extents) + cellHalfSize;
        Vector2 max = (center + extents) - cellHalfSize;

        CellIndex minCell = mapData.GetCellIndex(min);
        CellIndex maxCell = mapData.GetCellIndex(max);

        if(minCell.row + bottom <= maxCell.row)
            minCell.row += bottom;

        if(minCell.col + left <= maxCell.col)
            minCell.col += left;

        if(maxCell.row + top >= minCell.row)
            maxCell.row += top;

        if(maxCell.col + right >= minCell.col)
            maxCell.col += right;

        UpdateDimensions(minCell, maxCell);
    }

    public override bool EditIsPlacementValid() {
        return mEditIsValid;
    }

    public override void EditEnableCollision(bool aEnable) {
        mBody.simulated = aEnable;
    }

    protected override void ApplyMode(Mode prevMode) {
        switch(mode) {
            case Mode.None:
                break;

            case Mode.Ghost:
                mSpriteBodyDefaultColor = spriteBody.color;
                mSpriteFrameDefaultColor = spriteFrame.color;

                float a = ghostAlpha;

                spriteBody.color = new Color(mSpriteBodyDefaultColor.r, mSpriteBodyDefaultColor.g, mSpriteBodyDefaultColor.b, a);
                spriteFrame.color = new Color(mSpriteFrameDefaultColor.r, mSpriteFrameDefaultColor.g, mSpriteFrameDefaultColor.b, a);

                mBody.simulated = false;
                break;

            case Mode.Solid:
                spriteBody.color = mSpriteBodyDefaultColor;
                spriteFrame.color = mSpriteFrameDefaultColor;

                mBody.simulated = true;
                break;
        }
    }

    protected override void OnSpawned(GenericParams parms) {
        //default values
        mSpriteBodyDefaultColor = spriteBody.color;
        mSpriteFrameDefaultColor = spriteFrame.color;

        mCellSize = new CellIndex(1, 1);
        mBody.velocity = Vector2.zero;
        mBody.angularVelocity = 0f;

        ApplyCurrentCellSize();

        base.OnSpawned(parms);
    }

    protected override void OnDespawned() {
        spriteBody.color = mSpriteBodyDefaultColor;
        spriteFrame.color = mSpriteFrameDefaultColor;
    }

    protected override void Awake() {
        base.Awake();
        
        mBody = GetComponent<Rigidbody2D>();
        mColl = GetComponent<BoxCollider2D>();

        mBody.useAutoMass = true;
        mColl.density = density;

        mSpriteBodyBorderOfs = spriteBodyBorderPixelOfs / spriteBody.sprite.pixelsPerUnit;
    }

    void Update() {
        switch(mode) {
            case Mode.Ghost:
                float alpha = ghostAlpha;
                if(mEditIsValid) {
                    spriteBody.color = new Color(mSpriteBodyDefaultColor.r, mSpriteBodyDefaultColor.g, mSpriteBodyDefaultColor.g, alpha);
                    spriteFrame.color = new Color(mSpriteFrameDefaultColor.r, mSpriteFrameDefaultColor.g, mSpriteFrameDefaultColor.g, alpha);
                }
                else {
                    var clr = new Color(invalidColor.r, invalidColor.g, invalidColor.g, alpha);
                    spriteBody.color = clr;
                    spriteFrame.color = clr;
                }
                break;
        }
    }

    private void ApplyCurrentCellSize() {
        var cellSize = GameData.instance.blockSize;

        Vector2 size = new Vector2(mCellSize.col * cellSize.x, mCellSize.row * cellSize.y);
        Vector2 bodySize = new Vector2(size.x - mSpriteBodyBorderOfs*2f, size.y - mSpriteBodyBorderOfs*2f);

        spriteBody.size = bodySize;
        spriteFrame.size = size;
        mColl.size = size;
    }

    private void UpdateDimensions(CellIndex minCell, CellIndex maxCell) {
        var mapData = GameMapController.instance.mapData;

        var cellSize = GameData.instance.blockSize;

        CellIndex newCellSize = new CellIndex(maxCell.row - minCell.row + 1, maxCell.col - minCell.col + 1);

        Vector2 min = mapData.GetPositionFromCell(minCell);
        Vector2 max = mapData.GetPositionFromCell(maxCell); max += cellSize;

        Vector2 center = Vector2.Lerp(min, max, 0.5f);

        bool sizeChanged = mCellSize != newCellSize;
        bool posChanged = (Vector2)transform.position != center;

        if(sizeChanged) {
            mCellSize = newCellSize;

            ApplyCurrentCellSize();
        }

        if(posChanged) {
            transform.position = center;
        }

        if(sizeChanged || posChanged) {
            UpdatePlacementValid();

            DimensionChanged();
        }
    }
    
    private void UpdatePlacementValid() {
        //bool lastEditIsValid = mEditIsValid;
        
        //check pallete count
        //check overlap
        if(IsCountValid()) {
            var checkSize = new Vector2(mColl.size.x - overlapThreshold, mColl.size.y - overlapThreshold);

            var collider = Physics2D.OverlapBox(transform.position, checkSize, 0f, GameData.instance.blockInvalidMask);

            mEditIsValid = collider == null;
        }
        else
            mEditIsValid = false;

        //if(mEditIsValid != lastEditIsValid)
            UpdatePlacementValidDisplay(mEditIsValid);
    }

    private void UpdatePlacementValidDisplay(bool valid) {
        float alpha = mode == Mode.Ghost ? ghostAlpha : mSpriteBodyDefaultColor.a;

        if(valid) {
            spriteBody.color = new Color(mSpriteBodyDefaultColor.r, mSpriteBodyDefaultColor.g, mSpriteBodyDefaultColor.g, alpha);
            spriteFrame.color = new Color(mSpriteFrameDefaultColor.r, mSpriteFrameDefaultColor.g, mSpriteFrameDefaultColor.g, alpha);
        }
        else {
            var clr = new Color(invalidColor.r, invalidColor.g, invalidColor.g, alpha);
            spriteBody.color = clr;
            spriteFrame.color = clr;
        }
    }
}
