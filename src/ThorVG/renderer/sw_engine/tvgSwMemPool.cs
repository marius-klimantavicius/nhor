// Ported from ThorVG/src/renderer/sw_engine/tvgSwMemPool.cpp

namespace ThorVG
{
    public static class SwMemPool
    {
        public static unsafe SwOutline* mpoolReqOutline(SwMpool mpool, uint idx)
        {
            mpool.outline![idx].pts.Clear();
            mpool.outline[idx].cntrs.Clear();
            mpool.outline[idx].types.Clear();
            mpool.outline[idx].closed.Clear();

            fixed (SwOutline* ptr = &mpool.outline[idx])
            {
                return ptr;
            }
        }

        public static unsafe SwOutline* mpoolReqDashOutline(SwMpool mpool, uint idx)
        {
            // Dash outline uses same pool slot — in the C++ code this is
            // the same as mpoolReqOutline (allocated in same array).
            return mpoolReqOutline(mpool, idx);
        }

        public static SwStrokeBorder mpoolReqStrokeLBorder(SwMpool mpool, uint idx)
        {
            mpool.leftBorder![idx].pts.Clear();
            mpool.leftBorder[idx].start = -1;
            return mpool.leftBorder[idx];
        }

        public static SwStrokeBorder mpoolReqStrokeRBorder(SwMpool mpool, uint idx)
        {
            mpool.rightBorder![idx].pts.Clear();
            mpool.rightBorder[idx].start = -1;
            return mpool.rightBorder[idx];
        }

        public static SwCellPool mpoolReqCellPool(SwMpool mpool, uint idx)
        {
            return mpool.cellPool![idx];
        }

        public static SwMpool mpoolInit(uint threads)
        {
            var allocSize = threads + 1;

            var mpool = new SwMpool();
            mpool.outline = new SwOutline[allocSize];
            mpool.leftBorder = new SwStrokeBorder[allocSize];
            mpool.rightBorder = new SwStrokeBorder[allocSize];
            mpool.cellPool = new SwCellPool[allocSize];

            for (uint i = 0; i < allocSize; i++)
            {
                mpool.leftBorder[i] = new SwStrokeBorder();
                mpool.rightBorder[i] = new SwStrokeBorder();
                mpool.cellPool[i] = new SwCellPool();
            }

            mpool.allocSize = allocSize;
            return mpool;
        }

        public static void mpoolTerm(SwMpool? mpool)
        {
            if (mpool == null) return;

            mpool.outline = null;
            mpool.leftBorder = null;
            mpool.rightBorder = null;
            mpool.cellPool = null;
        }
    }
}
