using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;

public class MarketWebManager : MonoBehaviour
{
    [Header("📦 รายการสินค้าทั้งหมด")]
    public List<MarketListing> allListings;

    [Header("🖥️ หน้าเว็บหลัก")]
    public GameObject mainPageObj;
    public Transform gridContent;
    public MarketCardUI cardPrefab;

    [Header("🔍 หน้ารายละเอียด")]
    public GameObject detailsPageObj;
    public MarketDetailsUI detailsUI;

    [Header("แถบเมนู (Top Bar)")]
    public GameObject backButtonObj;

    [Header("🔎 ระบบค้นหาและตัวกรอง")]
    public TMP_InputField searchBar;
    public GameObject notFoundUI;

    private bool isViewingDetails = false;
    private int currentCategoryFilter = -1;

    void Start()
    {
        if (searchBar != null)
        {
            searchBar.onValueChanged.AddListener(SearchItems);
        }

        FilterByCategory(-1);
        ShowMainPage();
    }

    void Update()
    {
        if (isViewingDetails && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ShowMainPage();
        }
    }

    // =====================================
    // 🏷️ ระบบปุ่มกด Filter หมวดหมู่
    // =====================================
    public void FilterByCategory(int categoryID)
    {
        currentCategoryFilter = categoryID;

        string currentKeyword = searchBar != null ? searchBar.text : "";
        SearchItems(currentKeyword);
    }

    // =====================================
    // 🔎 ระบบกรองค้นหาสินค้า
    // =====================================
    public void SearchItems(string keyword)
    {
        // 🚨 ของใหม่: ถ้ากำลังดูหน้ารายละเอียดอยู่ แล้วมีการพิมพ์ค้นหา ให้เด้งกลับไปหน้าหลักทันที
        if (isViewingDetails)
        {
            ShowMainPage();
        }

        foreach (Transform child in gridContent)
        {
            Destroy(child.gameObject);
        }

        int matchCount = 0;
        string keywordLower = keyword.ToLower();

        foreach (var listing in allListings)
        {
            int itemCatID = (int)listing.baseItem.category;

            bool matchCategory = (currentCategoryFilter == -1) || (currentCategoryFilter == itemCatID);
            string itemNameLower = listing.GetDisplayName().ToLower();
            bool matchKeyword = string.IsNullOrEmpty(keyword) || itemNameLower.Contains(keywordLower);

            if (matchCategory && matchKeyword)
            {
                MarketCardUI card = Instantiate(cardPrefab, gridContent);
                card.Setup(listing, this);
                matchCount++;
            }
        }

        if (notFoundUI != null)
        {
            notFoundUI.SetActive(matchCount == 0);
        }
    }

    // =====================================
    // 🔀 ระบบสลับหน้าต่าง
    // =====================================
    public void OpenDetails(MarketListing listing)
    {
        isViewingDetails = true;

        mainPageObj.SetActive(false);
        detailsPageObj.SetActive(true);
        if (backButtonObj != null) backButtonObj.SetActive(true);

        detailsUI.Setup(listing);
    }

    public void ShowMainPage()
    {
        isViewingDetails = false;

        detailsPageObj.SetActive(false);
        mainPageObj.SetActive(true);
        if (backButtonObj != null) backButtonObj.SetActive(false);
    }
}