using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _App
{
    [DisallowMultipleComponent]
    public sealed class CombatStyleHudView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _rankLabel;
        [SerializeField] private TMP_Text _scoreLabel;
        [SerializeField] private TMP_Text _comboLabel;
        [SerializeField] private TMP_Text _varietyLabel;
        [SerializeField] private Image _rankProgress;

        internal void Render(in CombatStyleSnapshot snapshot, float rankProgress)
        {
            if (_canvasGroup == null || _rankLabel == null || _scoreLabel == null ||
                _comboLabel == null || _varietyLabel == null || _rankProgress == null)
            {
                return;
            }

            bool visible = snapshot.IsActive;
            _canvasGroup.alpha = visible ? 1.0f : 0.0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _rankLabel.SetText(GetRankLabel(snapshot.Rank));
            _rankLabel.color = GetRankColor(snapshot.Rank);
            _scoreLabel.SetText("STYLE {0:0}", snapshot.Score);
            if (snapshot.ComboHitCount == 1)
            {
                _comboLabel.SetText("1 HIT");
            }
            else
            {
                _comboLabel.SetText("{0} HITS", snapshot.ComboHitCount);
            }
            _varietyLabel.SetText("VARIETY {0}", snapshot.VarietyCount);
            _rankProgress.fillAmount = Mathf.Clamp01(rankProgress);
            _rankProgress.color = GetRankColor(snapshot.Rank);
        }

        private void Reset()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private static string GetRankLabel(CombatStyleRank rank)
        {
            return rank switch
            {
                CombatStyleRank.D => "D",
                CombatStyleRank.C => "C",
                CombatStyleRank.B => "B",
                CombatStyleRank.A => "A",
                CombatStyleRank.S => "S",
                CombatStyleRank.SS => "SS",
                CombatStyleRank.SSS => "SSS",
                _ => string.Empty
            };
        }

        private static Color GetRankColor(CombatStyleRank rank)
        {
            return rank switch
            {
                CombatStyleRank.C => new Color(0.35f, 0.8f, 1.0f),
                CombatStyleRank.B => new Color(0.35f, 1.0f, 0.55f),
                CombatStyleRank.A => new Color(1.0f, 0.82f, 0.25f),
                CombatStyleRank.S => new Color(1.0f, 0.45f, 0.16f),
                CombatStyleRank.SS => new Color(1.0f, 0.2f, 0.4f),
                CombatStyleRank.SSS => new Color(0.85f, 0.3f, 1.0f),
                _ => new Color(0.9f, 0.9f, 0.95f)
            };
        }
    }
}
