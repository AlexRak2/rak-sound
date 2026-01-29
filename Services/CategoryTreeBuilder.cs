using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SonnissBrowser
{
    public sealed class CategoryTreeBuilder
    {
        public void Rebuild(
            ObservableCollection<CategoryNode> categoryTree,
            ObservableCollection<string> categoryOptions,
            IEnumerable<SoundItem> sounds,
            Func<SoundItem, string> categorySelector)
        {
            categoryTree.Clear();
            categoryTree.Add(new CategoryNode("(All)", "(All)") { IsExpanded = true });

            BuildTree(categoryTree, sounds, categorySelector);

            categoryOptions.Clear();
            categoryOptions.Add(""); // blank = no override

            var list = new List<string>();
            foreach (var root in categoryTree)
                CollectKeys(root, list);

            foreach (var k in list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
                categoryOptions.Add(k);
        }

        private static void BuildTree(
            ObservableCollection<CategoryNode> root,
            IEnumerable<SoundItem> sounds,
            Func<SoundItem, string> categorySelector)
        {
            var lookup = new Dictionary<string, CategoryNode>(StringComparer.OrdinalIgnoreCase);

            var all = root.FirstOrDefault(n => n.Key == "(All)") ?? new CategoryNode("(All)", "(All)");
            if (!root.Contains(all)) root.Add(all);

            lookup[all.Key] = all;
            all.Children.Clear();
            all.Count = 0;

            foreach (var s in sounds)
            {
                var cat = categorySelector(s);
                if (string.IsNullOrWhiteSpace(cat)) cat = "Unsorted";

                all.Increment(1);

                var parts = cat.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) parts = new[] { "Unsorted" };

                string path = "";
                CategoryNode parent = all;

                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i].Trim();
                    if (part.Length == 0) continue;

                    path = (i == 0) ? part : (path + "/" + part);

                    if (!lookup.TryGetValue(path, out var node))
                    {
                        node = new CategoryNode(part, path);
                        parent.Children.Add(node);
                        lookup[path] = node;
                    }

                    node.Increment(1);
                    parent = node;
                }
            }

            all.IsExpanded = true;
        }

        private static void CollectKeys(CategoryNode node, List<string> list)
        {
            if (node.Key != "(All)")
                list.Add(node.Key);

            foreach (var c in node.Children)
                CollectKeys(c, list);
        }
    }
}
