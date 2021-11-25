using System.Collections.Generic;
using System.Linq;

namespace HNSW.Net
{
    /// <summary>
    /// Create filter for search in small world
    /// </summary>
    public interface FilterFactory
    {
        BaseFilter GetFilter();
    }

    /// <summary>
    /// Cachee for exclude processed ids
    /// </summary>
    internal sealed class FilterCachee
    {
        private readonly HashSet<int> _cacheeAllowed;
        private readonly HashSet<int> _cacheeDisallowed;

        internal FilterCachee()
        {
            _cacheeAllowed = new HashSet<int>();
            _cacheeDisallowed = new HashSet<int>();
        }

        /// <summary>
        /// Removing processed ids
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        internal IEnumerable<int> Exclude(IEnumerable<int> ids)
        {
            return ids.Where(id => _cacheeAllowed.Contains(id) == false && _cacheeDisallowed.Contains(id) == false);
        }

        /// <summary>
        /// Return all filtered ids includes new
        /// </summary>
        /// <param name="allowed">New filtered ids</param>
        /// <returns></returns>
        internal IEnumerable<int> Expand(IEnumerable<int> allowed)
        {
            // store new filtered (allowed) ids
            foreach (var id in allowed)
            {
                _cacheeAllowed.Add(id);
            }
            return _cacheeAllowed;
        }
        /// <summary>
        /// Remember new disallowed ids
        /// </summary>
        /// <param name="original"></param>
        internal void Keep(IEnumerable<int> original)
        {
            foreach (var id in original)
            {
                if (false == _cacheeAllowed.Contains(id))
                {
                    _cacheeDisallowed.Add(id);
                }
            }
        }
    }

    public abstract class BaseFilter
        : IFilter
    {
        private readonly bool _useCachee = false;

        public BaseFilter(bool useCachee)
        {
            _useCachee = useCachee;
            if (_useCachee)
            {
                _cachee = new FilterCachee();
            }
        }

        private readonly FilterCachee _cachee;

        public IEnumerable<int> Filter(IEnumerable<int> ids)
        {
            if (ids == null || ids.Any() == false) return ids;
            IEnumerable<int> original = null;
            if (_useCachee)
            {
                // store all entered ids
                original = ids.Select(id => id).ToArray();
                // deleting previously processed
                ids = _cachee.Exclude(ids);
            }
            if (ids.Any() == false) return _cachee.Expand(ids);
            // apply filter
            var cleaned = FilterByIds(ids);
            if (_useCachee)
            {
                // insert filtered ids and return all allowed
                cleaned = _cachee.Expand(cleaned);
                // store all ids
                _cachee.Keep(original);
                return cleaned;
            }
            return cleaned;
        }

        protected abstract IEnumerable<int> FilterByIds(IEnumerable<int> ids);
    }

    public interface IFilter
    {
        IEnumerable<int> Filter(IEnumerable<int> ids);
    }
}
