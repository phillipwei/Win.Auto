using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace win.auto
{
    public static class EqualityHelper
    {
        public static bool DictionaryContainsSameObjects<T, V>(Dictionary<T, V> dictionaryA, Dictionary<T, V> dictionaryB)
        {
            if (dictionaryA == null && dictionaryB == null)
            {
                return true;
            }
            else if (dictionaryA == null || dictionaryB == null)
            {
                return false;
            }
            else if (dictionaryA.Count != dictionaryB.Count)
            {
                return false;
            }

            // Compare keys
            foreach (T key in dictionaryA.Keys)
            {
                if (!dictionaryB.ContainsKey(key))
                {
                    return false;
                }
            }

            // Compare objects
            foreach (T key in dictionaryA.Keys)
            {
                if (!dictionaryA[key].Equals(dictionaryB[key]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IListsContainSameObjects<TValue>(IList<TValue> listA, IList<TValue> listB)
        {
            if (listA == null && listB == null)
            {
                return true;
            }
            else if (listA == null || listB == null)
            {
                return false;
            }
            else if (listA.Count != listB.Count)
            {
                return false;
            }
            foreach (TValue item in listA)
            {
                if (!listB.Contains(item))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool HashSetEquals<T>(HashSet<T> a, HashSet<T> b)
        {
            List<T> listA = a.ToList();
            List<T> listB = b.ToList();
            return IListEquals(listA, listB);
        }

        public static bool IListEquals<T>(IList<T> a, IList<T> b)
        {
            return IListEquals(a, b, null);
        }

        public static bool IListEquals<T>(IList<T> a, IList<T> b, Func<T, T, bool> equalityFunction)
        {
            if (a == null || b == null)
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (equalityFunction == null)
                {
                    if (!Object.Equals(a[i], b[i]))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!equalityFunction(a[i], b[i]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
