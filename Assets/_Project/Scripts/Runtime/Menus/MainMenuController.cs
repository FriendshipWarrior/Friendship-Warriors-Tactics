using System.Collections.Generic;
using FriendshipWarriors.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FriendshipWarriors.Menus
{
    /// <summary>
    /// The title screen: Quick Play (starts a random match), Campaign and Options (greyed out until they exist).
    /// Menus are kept as a stack of pages so submenus can slot in: Quick Play will later open a page of its own
    /// (online play, army building) before a match starts, and Back returns to the previous page.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string QuickPlayId = "quickplay", CampaignId = "campaign", OptionsId = "options";
        public const string Title = "Friendship Warriors Tactics";

        [SerializeField] private MenuScreen _screen;
        [SerializeField] private string _quickPlayScene = "QuickPlay";
        [SerializeField] private string _subtitle = "Beta demo";

        private readonly Stack<(MenuModel menu, string title, string subtitle)> _pages = new();

        private void OnEnable()
        {
            _screen.ItemActivated += OnItem;
            _screen.BackPressed += OnBack;
        }

        private void OnDisable()
        {
            _screen.ItemActivated -= OnItem;
            _screen.BackPressed -= OnBack;
        }

        private void Start()
        {
            var main = new MenuModel(Title);
            main.SetItems(new[]
            {
                new MenuItem(QuickPlayId, "Quick Play"),
                new MenuItem(CampaignId, "Campaign", enabled: false),
                new MenuItem(OptionsId, "Options", enabled: false),
            });
            OpenPage(main, Title, _subtitle);
        }

        private void OpenPage(MenuModel menu, string title, string subtitle)
        {
            _pages.Push((menu, title, subtitle));
            _screen.Show(menu, title, subtitle);
        }

        private void OnBack()
        {
            if (_pages.Count <= 1)
                return;
            _pages.Pop();
            var (menu, title, subtitle) = _pages.Peek();
            _screen.Show(menu, title, subtitle);
        }

        private void OnItem(MenuItem item)
        {
            switch (item.Id)
            {
                case QuickPlayId:
                    // For now Quick Play starts a random match straight away. Its submenu (online, army building)
                    // will be opened here with OpenPage instead.
                    SceneManager.LoadScene(_quickPlayScene);
                    break;
            }
        }
    }
}
