using System;
using System.Runtime.InteropServices;

namespace Optimization.Solver.GLPK
{
    /// <summary>
    /// Class contains definitions some functions exposed by GLPK.Dll
    /// </summary>
    /// <remarks>
    /// There are two types of glpk.dlls: The normal DLL and a modified DLL which writes errormessages to a file that will be read by this class.
    /// If you use the nomal DLL, users won't get specific error info about type of error an line where it occured
    /// </remarks>
    static class NativeFunctions
    {
        private const string DllLocation = @"glpk.dll";      

        // Model type and dimensions
        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int _glp_lpx_get_class(IntPtr lpx);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int _glp_lpx_get_num_cols(IntPtr lpx);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int _glp_lpx_get_num_rows(IntPtr lpx);

        // Columns info
        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr _glp_lpx_get_col_name(IntPtr lpx, int index);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int _glp_lpx_get_col_type(IntPtr lpx, int index);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double _glp_lpx_get_col_lb(IntPtr lpx, int index);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double _glp_lpx_get_col_ub(IntPtr lpx, int index);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double _glp_lpx_get_obj_coef(IntPtr lpx, int index);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int _glp_lpx_get_col_kind(IntPtr lpx, int index);

        // Row info
        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr _glp_lpx_get_row_name(IntPtr lpx, int index);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int _glp_lpx_get_row_type(IntPtr lpx, int index);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double _glp_lpx_get_row_lb(IntPtr lpx, int index);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double _glp_lpx_get_row_ub(IntPtr lpx, int index);

        // Nonzeros
        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int _glp_lpx_get_mat_row(IntPtr lpx, int rowindex, IntPtr colindices, IntPtr nzs);

        // Other
        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr glp_version();

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int _glp_lpx_get_obj_dir(IntPtr lpx);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr _glp_lpx_get_obj_name(IntPtr lpx);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void _glp_lpx_delete_prob(IntPtr lpx);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr _glp_lpx_read_model(String modfile, String datfile, String outfile);

        [DllImport(DllLocation, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr _glp_lpx_read_model2(String modfile, String datfile, String outfile);     // Only available in modified dll


        // Type of the structural variable:
        internal const int LPX_FR = 110;    // free variable 
        internal const int LPX_LO = 111;    // variable with lower bound 
        internal const int LPX_UP = 112;    // variable with upper bound 
        internal const int LPX_DB = 113;    // double-bounded variable 
        internal const int LPX_FX = 114;    // fixed variable 

        // Kind of the structural variable:
        internal const int LPX_CV = 160;    // continuous variable 
        internal const int LPX_IV = 161;    // integer variable 

        // Problem class: 
        internal const int LPX_LP  = 100;   // linear programming (LP) 
        internal const int LPX_MIP = 101;   // mixed integer programming (MIP) 

        // Optimization direction flag (objective "sense"):
        internal const int LPX_MI = 120;    // minimization
        internal const int LPX_MA = 121;    // maximization
    }
}
