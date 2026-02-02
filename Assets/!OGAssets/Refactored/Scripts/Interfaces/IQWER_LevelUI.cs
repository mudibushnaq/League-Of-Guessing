using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public interface IQWER_LevelUI
{
    void HideStreakTimer();
    void SetSkillSprites(Sprite q, Sprite w, Sprite e, Sprite r);
    void ClearSlots();
    void ClearKeyboard();
    void SetLevelText(string txt);
    void SetInputEnabled(bool on);
    void SetKeyboardVisible(bool visible);
    UniTask<int> RevealRemainingSkillsSequentialAsync(int mask);
    void ApplySkillMask(int mask);
    void RefreshUnlockButtons(int mask);
    UniTask FlipOutAsync();
    void SetAllUnlockButtonsVisible(bool visible);
    void SetUnlockButtonsInteractable(bool on);
    void SetUnlockVisual(int index, bool useAdMode, int keyCost);
    Button GetUnlockButton(int i);
    SkillCell GetSkillCell(int i);
    void BuildSlotsForAnswer(string displayName);
    void HideSlotsImmediate();
    UniTask FlipInAsync();

    UniTask<int> PlayButtonRouletteAndRevealOneAsync(int existingMask);
    UniTask ShowSlotsStaggerAsync();
    void BuildKeyboardWithStagger(List<char> pool, Action<char, LetterButton> onKey);
    UniTask PlayCurrentAnswerFXAsync();
    UniTask PlayStreakTierFXAsync(int tier, int lpAward);
    UniTask PlayWrongGuessFXAsync(bool isShutDown);
    void ClearSlotAt(int slotIndex);
    void FocusFirstEmptySlot();
    void ShowStreakTimer(float windowSeconds);
    void UpdateStreakTimer(float remainingSeconds, float windowSeconds);
    void BindUnlockButtons(Action<int> onClick);
    void BuildKeyboard(List<char> letters, Action<char, LetterButton> onPressed);
    int  GetLetterSlotCount();
    bool IsLetterSlotEmptyAtPos(int pos);      // pos is letter index in the normalized answer
    void ClearLetterSlotAtPos(int pos);        // clears and focuses that position
    char GetTargetLetterAtPos(int pos);        // normalized target letter at pos
    int  GetGlobalSlotIndexForPos(int pos);    // maps letter-pos -> global slot index (into _slots)
    //IEnumerable<LetterButton> GetKeyboardButtons(); // iterate current keyboard buttons
    IReadOnlyList<LetterButton> GetKeyboardButtons();
    void SetNormalizedAnswer(string normalized);
    int FillNextEmpty(char c);
    string ReadCurrentGuess();
    event Action<int> SlotClicked;
    void SetArtContainerBG(Sprite bg) { }

}
