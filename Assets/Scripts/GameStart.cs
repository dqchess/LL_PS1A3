﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameStart : MonoBehaviour {
    public GameObject loadingGO;
    public GameObject readyGO;

    public GameObject titleGO;
    public Text titleText;
    [M8.Localize]
    public string titleStringRef;

    void Awake() {
        loadingGO.SetActive(true);
        readyGO.SetActive(false);
        titleGO.SetActive(false);
    }

    IEnumerator Start () {
        //wait for language to be loaded
        while(!LoLLocalize.instance.isLoaded)
            yield return null;

        //start title
        titleText.text = LoLLocalize.Get(titleStringRef);
        titleGO.SetActive(true);

        //wait for LoL to load/initialize
        while(!LoLManager.instance.isReady)
            yield return null;

        loadingGO.SetActive(false);
        readyGO.SetActive(true);
    }
}
