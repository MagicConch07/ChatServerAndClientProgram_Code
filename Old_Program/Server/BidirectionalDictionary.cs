using System;
using System.Collections;
using System.Collections.Generic;

namespace ChatServer
{
    public class BidirectionalDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private Dictionary<TKey, TValue> _forward = new Dictionary<TKey, TValue>();
        private Dictionary<TValue, TKey> _reverse = new Dictionary<TValue, TKey>();

        public TValue this[TKey key]
        {
            get => _forward[key];
            set
            {
                if (_forward.ContainsKey(key))
                {
                    var oldValue = _forward[key];
                    _reverse.Remove(oldValue);
                }

                if (_reverse.ContainsKey(value))
                    throw new ArgumentException("Value already exists with another key.");

                _forward[key] = value;
                _reverse[value] = key;
            }
        }

        public ICollection<TKey> Keys => _forward.Keys;

        public ICollection<TValue> Values => _forward.Values;

        public int Count => _forward.Count;

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            if (_forward.ContainsKey(key) || _reverse.ContainsKey(value))
                throw new ArgumentException("Duplicate key or value.");

            _forward.Add(key, value);
            _reverse.Add(value, key);
        }

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        public void Clear()
        {
            _forward.Clear();
            _reverse.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return _forward.TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
        }

        public bool ContainsKey(TKey key) => _forward.ContainsKey(key);

        public bool ContainsValue(TValue value) => _reverse.ContainsKey(value);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            foreach (var pair in _forward)
            {
                array[arrayIndex++] = pair;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _forward.GetEnumerator();

        public bool Remove(TKey key)
        {
            if (!_forward.TryGetValue(key, out var value))
                return false;

            _forward.Remove(key);
            _reverse.Remove(value);
            return true;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (Contains(item))
            {
                return Remove(item.Key);
            }
            return false;
        }

        public bool RemoveKeyByValue(TValue value)
        {
            if (!_reverse.TryGetValue(value, out var key))
                return false;

            _reverse.Remove(value);
            _forward.Remove(key);
            return true;
        }

        public bool TryGetValue(TKey key, out TValue value) => _forward.TryGetValue(key, out value);

        public bool TryGetKeyByValue(TValue value, out TKey key) => _reverse.TryGetValue(value, out key);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
