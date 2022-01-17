﻿using BlazorComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MASA.Blazor
{
    public class MPageTabs : MTabs, IPageTabs
    {
        [EditorRequired]
        [Parameter]
        public IList<PageTabItem> Items { get; set; }

        [Parameter]
        public string CloseIcon { get; set; } = "mdi-close";

        [Parameter]
        public RenderFragment<PageTabContentContext> TabContent { get; set; }

        [Inject]
        public NavigationManager NavigationManager { get; set; }

        IList<PageTabItem> IPageTabs.ComputedItems => ComputedItems;

        bool IPageTabs.NoActiveItem => NoActiveItem;

        bool IPageTabs.IsActive(PageTabItem item) => IsActive(item);

        Task IPageTabs.HandleOnOnReloadAsync(MouseEventArgs args) => HandleOnOnReloadAsync(args);

        Task IPageTabs.HandleOnCloseLeftAsync(MouseEventArgs args) => HandleOnCloseLeftAsync(args);

        Task IPageTabs.HandleOnCloseRightAsync(MouseEventArgs args) => HandleOnCloseRightAsync(args);

        Task IPageTabs.HandleOnCloseOtherAsync(MouseEventArgs args) => HandleOnCloseOtherAsync(args);

        protected IList<PageTabItem> ComputedItems
        {
            get
            {
                return Items.Where(PageTabItemManager.IsOpened).OrderBy(PageTabItemManager.OpenedTime).ToList();
            }
        }

        protected bool NoActiveItem => !ComputedItems.Any(IsActive);

        protected string CurrentUrl => NavigationManager.ToBaseRelativePath(NavigationManager.Uri);

        protected bool IsMenuActive { get; set; }

        protected (double X, double Y) MenuPosition { get; set; }

        protected PageTabItem MenuActiveItem { get; set; }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            //REVIEW: Is this ok? 
            HideSlider = true;
            NavigationManager.LocationChanged += OnLocationChanged;

            //By default,open the first page
            var firstItem = Items.FirstOrDefault();
            PageTabItemManager.Open(firstItem);
        }

        private void OnLocationChanged(object sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
        {
            var url = NavigationManager.ToBaseRelativePath(e.Location);
            var item = Items.FirstOrDefault(item => item.Url == url);

            if (item != null && !PageTabItemManager.IsOpened(item))
            {
                PageTabItemManager.Open(item);
                InvokeStateHasChanged();
            }
        }

        protected override void SetComponentClass()
        {
            base.SetComponentClass();

            CssProvider
                .Apply("page-item", styleAction: styleBuilder =>
                {
                    var isActive = (bool)styleBuilder.Data;
                    styleBuilder
                        .AddIf("display:none", () => !isActive);
                });

            AbstractProvider
                .ApplyPageTabsDefault()
                .Merge(typeof(BTab), typeof(MTab), attrs =>
                {
                    var item = (PageTabItem)attrs.Data;
                    attrs["oncontextmenu"] = CreateEventCallback<MouseEventArgs>(async args => await ShowMenuAsync(args.ClientX, args.ClientY, item));
                    attrs["__internal_preventDefault_oncontextmenu"] = true;
                })
                .Apply(typeof(BMenu), typeof(MMenu), attrs =>
                {
                    attrs[nameof(BMenu.Value)] = IsMenuActive;
                    attrs[nameof(MMenu.ValueChanged)] = CreateEventCallback<bool>(val =>
                    {
                        IsMenuActive = val;
                    });
                    attrs[nameof(BMenu.PositionX)] = MenuPosition.X;
                    attrs[nameof(BMenu.PositionY)] = MenuPosition.Y;
                })
                .Apply(typeof(BIcon), typeof(MIcon), attrs =>
                {
                    var item = (PageTabItem)attrs.Data;
                    attrs[nameof(MIcon.OnClick)] = CreateEventCallback<MouseEventArgs>(async args => await CloseAsync(item));
                    attrs["__internal_stopPropagation_onclick"] = true;
                });
        }

        protected async Task ShowMenuAsync(double clientX, double clientY, PageTabItem item)
        {
            //We will change this when menu been refactored
            MenuActiveItem = item;
            IsMenuActive = false;
            await Task.Delay(16);

            MenuPosition = (clientX, clientY);
            IsMenuActive = true;
        }

        protected Task CloseAsync(PageTabItem item)
        {
            Debug.Assert(item != null);

            PageTabItemManager.Close(item);
            if (IsActive(item))
            {
                //Active item has been closed,goto first or default
                var lastItem = ComputedItems.FirstOrDefault();
                NavigationManager.NavigateTo(lastItem?.Url ?? "");
            }

            return Task.CompletedTask;
        }

        protected bool IsActive(PageTabItem item)
        {
            return item.Url == CurrentUrl;
        }

        protected Task HandleOnOnReloadAsync(MouseEventArgs args)
        {
            PageTabItemManager.Close(MenuActiveItem);
            NavigationManager.NavigateTo(MenuActiveItem.Url);

            return Task.CompletedTask;
        }

        protected async Task HandleOnCloseLeftAsync(MouseEventArgs args)
        {
            var startIndex = 0;
            var endIndex = ComputedItems.IndexOf(MenuActiveItem);

            if (endIndex > startIndex)
            {
                var items = ComputedItems.Take(endIndex);
                foreach (var item in items)
                {
                    await CloseAsync(item);
                }
            }
        }

        protected async Task HandleOnCloseRightAsync(MouseEventArgs args)
        {
            var startIndex = ComputedItems.IndexOf(MenuActiveItem) + 1;
            var endIndex = ComputedItems.Count;

            if (endIndex > startIndex)
            {
                var items = ComputedItems.Skip(startIndex).Take(endIndex - startIndex);
                foreach (var item in items)
                {
                    await CloseAsync(item);
                }
            }
        }

        protected async Task HandleOnCloseOtherAsync(MouseEventArgs args)
        {
            await HandleOnCloseLeftAsync(args);
            await HandleOnCloseRightAsync(args);
        }

        protected override void Dispose(bool disposing)
        {
            NavigationManager.LocationChanged -= OnLocationChanged;
            base.Dispose(disposing);
        }
    }
}