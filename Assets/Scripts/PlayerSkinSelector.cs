using System.Collections.Generic;
using UnityEngine;

public class PlayerSkinSelector : MonoBehaviour
{
    [SerializeField] private GameObject[] skins;
    [SerializeField] private bool autoFindChildren = true;

    private int _currentIndex = -1;

    public int SkinCount => (skins != null) ? skins.Length : 0;

    private void Awake()
    {
        if (autoFindChildren && (skins == null || skins.Length == 0))
            CacheChildren();

        // Ocultar todas desde el inicio para evitar stack visual antes de Spawned()
        if (skins != null)
            for (int i = 0; i < skins.Length; i++)
                if (skins[i] != null) skins[i].SetActive(false);
    }

    private void OnValidate()
    {
        if (autoFindChildren && (skins == null || skins.Length == 0))
            CacheChildren();
    }

    public void SetSkinIndex(int index)
    {
        if (skins == null || skins.Length == 0) return;

        int clamped = Mathf.Clamp(index, 0, skins.Length - 1);
        if (_currentIndex == clamped) return;

        for (int i = 0; i < skins.Length; i++)
            skins[i].SetActive(i == clamped);

        _currentIndex = clamped;
    }

    private void CacheChildren()
    {
        var list = new List<GameObject>(transform.childCount);
        for (int i = 0; i < transform.childCount; i++)
            list.Add(transform.GetChild(i).gameObject);

        skins = list.ToArray();
    }
}
