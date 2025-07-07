namespace ChatServer
{
    class NickNameManager : Singleton<NickNameManager>
    {
        private BidirectionalDictionary<int, string> _pcNickNameDictinoary;

        public NickNameManager()
        {
            _pcNickNameDictinoary = new BidirectionalDictionary<int, string>();
        }

        public bool IsValidNickName(string nickName)
        {
            return _pcNickNameDictinoary.ContainsValue(nickName);
        }

        public bool TryGetNickName(int id, out string nickName)
        {
            return _pcNickNameDictinoary.TryGetValue(id, out nickName);
        }

        public bool TryGetClientId(string nickName, out int id)
        {
            return _pcNickNameDictinoary.TryGetKeyByValue(nickName, out id);
        }

        public bool TryAddNickName(int id, string nickName)
        {
            if(_pcNickNameDictinoary.ContainsKey(id) || _pcNickNameDictinoary.ContainsValue(nickName))
            {
                return false;
            }

            _pcNickNameDictinoary.Add(id, nickName);

            return true;
        }

        public void RemoveNickName(int id)
        {
            _pcNickNameDictinoary.Remove(id);
        }
    }
}
