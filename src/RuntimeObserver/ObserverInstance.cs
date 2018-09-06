namespace RuntimeObserver
{
    public static class ObserverInstance
    {
        private static Observer instance;

        public static Observer Get()
        {
            return instance;
        } 

        public static void Start()
        {
            instance = new Observer();
        }
    }
}
