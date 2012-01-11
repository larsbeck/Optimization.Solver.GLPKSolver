using System;
using System.Collections.Generic;
using System.Linq;
using Optimization.Interfaces;
using Optimization.Solver.Events;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;

namespace Optimization.Solver.GLPK
{
    /// <summary>
    /// GNU Linear Programming Kit (GLPK) Solver Interface for the Optimization.Framework
    /// </summary>
    /// <see cref="http://www.gnu.org/s/glpk/"/>
    /// 
    /// <seealso cref="http://anna-at-work.bplaced.net/?p=31"/>
    /// <seealso cref="http://www.optimizationzen.com"/>
    /// 
    /// <author>SG</author>
    unsafe public class GLPKSolver : ISolver
    {
        private const string glpkLib = @"glpk.dll";
        //private const string sDebugDirectory = @"C:\Temp\";

        private const int GLP_ON = 1;  /* enable something */
        private const int GLP_OFF = 0;  /* disable something */

        #region Enums and structures from glpk.h

        public struct glp_tree
        {
            double _opaque_tree;
        }

        public unsafe struct glp_iocp
        {     /* integer optimizer control parameters */
            public int msg_lev;            /* message level (see glp_smcp) */
            public int br_tech;            /* branching technique: */
            public const int GLP_BR_FFV = 1;  /* first fractional variable */
            public const int GLP_BR_LFV = 2;  /* last fractional variable */
            public const int GLP_BR_MFV = 3;  /* most fractional variable */
            public const int GLP_BR_DTH = 4;  /* heuristic by Driebeck and Tomlin */
            public const int GLP_BR_PCH = 5;  /* hybrid pseudocost heuristic */
            public int bt_tech;            /* backtracking technique: */
            public const int GLP_BT_DFS = 1;  /* depth first search */
            public const int GLP_BT_BFS = 2;  /* breadth first search */
            public const int GLP_BT_BLB = 3;  /* best local bound */
            public const int GLP_BT_BPH = 4;  /* best projection heuristic */
            public double tol_int;         /* mip.tol_int */
            public double tol_obj;         /* mip.tol_obj */
            public int tm_lim;             /* mip.tm_lim (milliseconds) */
            public int out_frq;            /* mip.out_frq (milliseconds) */
            public int out_dly;            /* mip.out_dly (milliseconds) */
            //-> void (*cb_func)(glp_tree *T, void *info);
            public void* cb_func;
            /* mip.cb_func */
            public void* cb_info;          /* mip.cb_info */
            public int cb_size;            /* mip.cb_size */
            public int pp_tech;            /* preprocessing technique: */
            public const int GLP_PP_NONE = 0;  /* disable preprocessing */
            public const int GLP_PP_ROOT = 1;  /* preprocessing only on root level */
            public const int GLP_PP_ALL = 2;  /* preprocessing on all levels */
            public double mip_gap;         /* relative MIP gap tolerance */
            public int mir_cuts;           /* MIR cuts       (GLP_ON/GLP_OFF) */
            public int gmi_cuts;           /* Gomory's cuts  (GLP_ON/GLP_OFF) */
            public int cov_cuts;           /* cover cuts     (GLP_ON/GLP_OFF) */
            public int clq_cuts;           /* clique cuts    (GLP_ON/GLP_OFF) */
            public int presolve;           /* enable/disable using MIP presolver */
            public int binarize;           /* try to binarize integer variables */
            public int fp_heur;            /* feasibility pump heuristic */
            //#if 1 /* 28/V-2010 */
            public int alien;              /* use alien solver */
            //#endif
            //public double[] foo_bar;     /* (reserved) */
        }

        private enum GLP_SOLUTIONSTATUS
        {
            GLP_UNDEF = 1,  /* solution is undefined */
            GLP_FEAS = 2,  /* solution is feasible */
            GLP_INFEAS = 3,  /* solution is infeasible */
            GLP_NOFEAS = 4,  /* no feasible solution exists */
            GLP_OPT = 5,  /* solution is optimal */
            GLP_UNBND = 6  /* solution is unbounded */
        }
        private enum GLP_ROWTYPE
        {
            GLP_FR = 1,  /* free variable */
            GLP_LO = 2,  /* variable with lower bound */
            GLP_UP = 3,  /* variable with upper bound */
            GLP_DB = 4,  /* double-bounded variable */
            GLP_FX = 5  /* fixed variable */
        }

        private enum OBJECTIVE_TYPE
        {
            /* optimization direction flag: */
            GLP_MIN = 1,  /* minimization */
            GLP_MAX = 2  /* maximization */
        }

        private enum MPS_FILETYPE
        {
            GLP_MPS_DECK = 1,  /* fixed (ancient) */
            GLP_MPS_FILE = 2  /* free (modern) */
        }

        private enum GLP_VARIABLE_KIND
        {
            GLP_CV = 1,  /* continuous variable */
            GLP_IV = 2,  /* integer variable */
            GLP_BV = 3  /* binary variable */
        }

        #endregion

        #region GLPK DLL Imports

        [DllImport(glpkLib, SetLastError = true)]
        unsafe static extern double* glp_create_prob();

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern void glp_init_iocp(void* parm);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern void glp_term_hook(void* func, void* info);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int glp_write_mip(double* mip, string fname);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern double glp_mip_obj_val(double* mip);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern void glp_set_col_kind(double* mip, int j, int kind);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern double glp_mip_col_val(double* mip, int j);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int glp_mip_status(double* mip);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int glp_intopt(double* mip, void* parm);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern void glp_set_prob_name(double* lp, string name);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern void glp_set_obj_name(double* lp, string name);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern void glp_set_row_name(double* lp, int i, string name);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int glp_write_lp(double* lp, double* parm, string fname);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int glp_get_status(double* lp);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern void glp_set_col_bnds(double* lp, int j, int type, double lb, double ub);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern void glp_set_obj_dir(double* lp, int dir);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern double glp_get_col_prim(double* lp, int j);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern double glp_get_obj_val(double* lp);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern void glp_set_obj_coef(double* lp, int j, double coef);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern void glp_set_row_bnds(double* lp, int i, int type, double lb, double ub);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int glp_add_rows(double* lp, int rows);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int glp_add_cols(double* lp, int cols);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int glp_get_num_rows(double* lp);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int glp_get_num_cols(double* lp);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int glp_simplex(double* lp, double* param);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern void glp_load_matrix(double* lp, int ne, int* ia, int* ja, double* ar);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern int glp_write_mps(double* lp, int fmt, double* parm, string fname);

        [DllImport(glpkLib, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern void glp_set_col_name(double* lp, int j, string name);
        #endregion

        // Reference to the Optimization.Framework model
        private IModel _model;

        // Pointer to the GLPK internal model
        private double* _glpk_model;

        // Array of row indices
        private ArrayList ia = new ArrayList();

        // Array of column indices
        private ArrayList ja = new ArrayList();

        // Array of coefficients
        private ArrayList ar = new ArrayList();

        private Action<string> WriteLogLine;

        /// <summary>
        /// True, if model is a (mixed) integer model
        /// </summary>
        public bool IsMixedIntegerModel { get; private set; }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void CallbackDelegate(void* info, string s);

        void CallbackOutput(void* info, string s)
        {
            WriteLogLine(s.Replace('\n', ' '));
        }

        // Define configuration structure
        public glp_iocp IOCP;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public GLPKSolver()
        {
            SetupHelpers.SetPathForSolverLib("GLPK");
            if (WriteLogLine == null)
                WriteLogLine = nulloutput;
        }

        private static void nulloutput(string output)
        {
        }


        /// <summary>
        /// Initializes a new instance of the GLPKLinearSolver class.
        /// </summary>
        unsafe public GLPKSolver(Action<string> writeLog):this()
        {
            WriteLogLine = writeLog;
            /*
            // Hook to terminal and get GLPK output
            var cd = new CallbackDelegate(this.CallbackOutput);
            IntPtr ptr = Marshal.GetFunctionPointerForDelegate(cd);
            glp_term_hook(ptr.ToPointer(),null);

            // Configure GLPK MIP solver
            fixed (void* iocc = &this.IOCP)
            {
                //glp_init_iocp(iocc);
            }
            IOCP.pp_tech = glp_iocp.GLP_PP_ROOT;
            IOCP.mir_cuts = GLP_ON;
            IOCP.gmi_cuts = GLP_ON;
            IOCP.cov_cuts = GLP_ON;
            IOCP.clq_cuts = GLP_ON;
            IOCP.presolve = GLP_ON;
            */
            IsBusy = false;
        }

        #region ISolver Members

        /// <summary>
        /// The configuration of this solver instance.
        /// </summary>
        /// <value></value>
        public ISolverConfiguration Configuration
        {
            get
            {
                throw new NotImplementedException("Configuration not supported yet");
            }
            set
            {
                if (IsBusy)
                    throw new InvalidOperationException("solver is busy");

                throw new NotImplementedException("Configuration not supported yet");
            }
        }


        /// <summary>
        /// Is this solver instance busy?
        /// </summary>
        /// <value></value>
        public bool IsBusy { get; private set; }

        /// <summary>
        /// Deletes the internal datastructures of this solver instance.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If this solver instance is busy.</exception>
        public void ClearLastModel()
        {

            if (IsBusy != true)
            {
                _model = null;
                ia = new ArrayList();
                ja = new ArrayList();
                ar = new ArrayList();
            }
            else
            {
                throw new InvalidOperationException();
            }
        }


        /// <summary>
        /// Solves the specified model.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="variableValues">The variable values.</param>
        /// <returns>A solution object.</returns>
        public ISolution Solve(IModel model, IDictionary<string, double> variableValues = null)
        {
            if (model == null)
                throw new ArgumentNullException("model");
            if (IsBusy)
                throw new InvalidOperationException("solver is busy");
            try
            {
                IsBusy = true;
                if (variableValues == null)
                    variableValues = new Dictionary<string, double>();

                // Time measurement
                DateTime overallWallTimeStart = DateTime.Now;

                // Store the indices used by GLPK to map the solution
                Dictionary<IVariable, int> variablesToIdx = new Dictionary<IVariable, int>();

                // Create an empty GLPK model
                _model = model;
                _glpk_model = glp_create_prob();

                glp_set_prob_name(_glpk_model, model.Name);
                WriteLogLine(" - Creating GLPK model");
                // Create internal GLPK variables from model's variable list
                WriteLogLine(" - Creating GLPK variables");
                int colIndex = glp_add_cols(_glpk_model, model.VariablesCount); // returns first column index
                foreach (IVariable variable in model.Variables)
                {
                    if (variable.Type != VariableType.Continuous)
                        this.IsMixedIntegerModel = true;
                    //throw new ArgumentException("MIPs/ IPs not supported yet");

                    // Check for fixed variables
                    if (variableValues.ContainsKey(variable.Name))
                    {
                        variable.LowerBound = variableValues[variable.Name];
                        variable.UpperBound = variableValues[variable.Name];
                    }

                    // Map variables bound types to GLPK's internal structures
                    GLP_ROWTYPE glpkType;
                    if (double.IsNegativeInfinity(variable.LowerBound) && double.IsPositiveInfinity(variable.UpperBound))
                        glpkType = GLP_ROWTYPE.GLP_FR;
                    else if (!double.IsNegativeInfinity(variable.LowerBound) &&
                             double.IsPositiveInfinity(variable.UpperBound))
                        glpkType = GLP_ROWTYPE.GLP_LO;
                    else if (double.IsNegativeInfinity(variable.LowerBound) &&
                             !double.IsPositiveInfinity(variable.UpperBound))
                        glpkType = GLP_ROWTYPE.GLP_UP;
                    else if (variable.LowerBound == variable.UpperBound)
                        glpkType = GLP_ROWTYPE.GLP_FX;
                    else
                        glpkType = GLP_ROWTYPE.GLP_DB;

                    // Set bound type and bounding values; depending on the type bounds might be ignored by GLPK
                    glp_set_col_bnds(_glpk_model, colIndex, (int)glpkType, variable.LowerBound, variable.UpperBound);

                    // Set the variable's name
                    glp_set_col_name(_glpk_model, colIndex, variable.Name);

                    // Set the variable's kind
                    switch (variable.Type)
                    {
                        case VariableType.Continuous:
                            glp_set_col_kind(_glpk_model, colIndex, (int)GLP_VARIABLE_KIND.GLP_CV);
                            break;
                        case VariableType.Integer:
                            if (variable.UpperBound == 1 && variable.LowerBound == 0)
                                glp_set_col_kind(_glpk_model, colIndex, (int)GLP_VARIABLE_KIND.GLP_BV);
                            else
                                glp_set_col_kind(_glpk_model, colIndex, (int)GLP_VARIABLE_KIND.GLP_IV);
                            break;
                        default:
                            throw new NotImplementedException("Variable type not supported by OptimizationFramework");
                            break;
                    }

                    // Store the used index for later activity mapping
                    variablesToIdx.Add(variable, colIndex);

                    colIndex++;
                }

                // Create internal GLPK objective function
                WriteLogLine(" - Creating GLPK objective function");
                if (model.ObjectivesCount != 0)
                {
                    if (model.ObjectivesCount > 1)
                        throw new ArgumentException("Only one objective supported");

                    IObjective objective = model.Objectives.ElementAt(0);

                    // Loop through all expressions and set the variable's coefficient
                    foreach (var term in objective.Expression.Terms)
                    {
                        if (!term.isLinear)
                            throw new ArgumentException("Only linear terms in objective allowed: " + term);
                        glp_set_obj_coef(_glpk_model, variablesToIdx[term.Variable], term.Factor);
                    }

                    // Set objective's name
                    glp_set_obj_name(_glpk_model, objective.Name);

                    // Set GLPKs objective type
                    if (objective.Sense == ObjectiveSense.Maximize)
                        glp_set_obj_dir(_glpk_model, (int)OBJECTIVE_TYPE.GLP_MAX);
                    else
                        glp_set_obj_dir(_glpk_model, (int)OBJECTIVE_TYPE.GLP_MIN);
                }

                // Create GLPK constraints and reserve space for all of them
                WriteLogLine(" - Creating GLPK constraints");
                int rowIndex = glp_add_rows(_glpk_model, model.ConstraintsCount); // Returns the first rowIndex
                // Dummy constraint since GLPKs arrays start from 1
                ia.Add(-1);
                ja.Add(-1);
                ar.Add(-1d);

                // Loop through all constraints in the model
                foreach (IConstraint constraint in model.Constraints)
                {
                    // Loop through each term
                    foreach (var term in constraint.Expression.Terms)
                    {
                        // Add coefficient (factor) for matrix cell [i,j], i=rowIndex, j=variablesToIdx[term.Variable]
                        ia.Add(rowIndex);
                        ja.Add(variablesToIdx[term.Variable]);
                        ar.Add(term.Factor);
                    }

                    // Map constraint bound types to GLPK's internal structures
                    GLP_ROWTYPE glpkConstraintType;
                    if (double.IsNegativeInfinity(constraint.LowerBound) &&
                        double.IsPositiveInfinity(constraint.UpperBound))
                        glpkConstraintType = GLP_ROWTYPE.GLP_FR;
                    else if (!double.IsNegativeInfinity(constraint.LowerBound) &&
                             double.IsPositiveInfinity(constraint.UpperBound))
                        glpkConstraintType = GLP_ROWTYPE.GLP_LO;
                    else if (double.IsNegativeInfinity(constraint.LowerBound) &&
                             !double.IsPositiveInfinity(constraint.UpperBound))
                        glpkConstraintType = GLP_ROWTYPE.GLP_UP;
                    else if (constraint.LowerBound == constraint.UpperBound)
                        glpkConstraintType = GLP_ROWTYPE.GLP_FX;
                    else
                        glpkConstraintType = GLP_ROWTYPE.GLP_DB;

                    // Since the OF moves LBs/ UBs to constant terms and sets the constraint's bound to 0, 
                    // we have to add the constant to the bound again
                    if (constraint.Expression.Constant < 0)
                        constraint.UpperBound -= constraint.Expression.Constant;
                    else if (constraint.Expression.Constant > 0)
                        constraint.LowerBound += constraint.Expression.Constant;

                    // Set bounds for the constraint
                    glp_set_row_bnds(_glpk_model, rowIndex, (int)glpkConstraintType, constraint.LowerBound,
                                     constraint.UpperBound);
                    glp_set_row_name(_glpk_model, rowIndex, constraint.Name);
                    rowIndex++;
                }

                // Solve actual problem
                ModelStatus modelStatus;
                SolutionStatus solutionStatus;

                // Print some logging data
                WriteLogLine(String.Format(" - Model has {0} variables", model.VariablesCount));
                WriteLogLine(String.Format(" - Model has {0} integer variables", model.Variables.Where(v => v.Type == VariableType.Integer).Count()));
                WriteLogLine(String.Format(" - Model has {0} constraints", model.ConstraintsCount));
                WriteLogLine(String.Format(" - Model has {0} non zero elements", ia.Count));

                //// Try to write MPS file
                //if (Directory.Exists(sDebugDirectory))
                //{
                //    try
                //    {
                //        glp_write_lp(_glpk_model, null, sDebugDirectory + model.Name + ".lp");
                //        glp_write_mps(_glpk_model, (int)MPS_FILETYPE.GLP_MPS_FILE, null,
                //                     sDebugDirectory + model.Name + ".mps");
                //        WriteLogLine(" - Saved models in " + sDebugDirectory);
                //    }
                //    catch (Exception e)
                //    {
                //        WriteLogLine("ERROR: Could not write model files: " + e.Message);
                //    }
                //}

                WriteLogLine(" - Solving GLPK model");
                bool returnValue = SolveProblem(_glpk_model, ia, ja, ar);

                DateTime overallWallTimeEnd = DateTime.Now;
                TimeSpan overallWallTime = overallWallTimeEnd - overallWallTimeStart;

                // Set Optimization.Framework's solution status based on the GLPK structures
                GLP_SOLUTIONSTATUS status;
                if (this.IsMixedIntegerModel)
                    status = (GLP_SOLUTIONSTATUS)glp_mip_status(_glpk_model);
                else
                    status = (GLP_SOLUTIONSTATUS)glp_get_status(_glpk_model);

                switch (status)
                {
                    case GLP_SOLUTIONSTATUS.GLP_NOFEAS:
                    case GLP_SOLUTIONSTATUS.GLP_INFEAS:
                        solutionStatus = SolutionStatus.NoSolutionValues;
                        modelStatus = ModelStatus.Infeasible;
                        break;
                    case GLP_SOLUTIONSTATUS.GLP_UNBND:
                        solutionStatus = SolutionStatus.NoSolutionValues;
                        modelStatus = ModelStatus.Unbounded;
                        break;
                    case GLP_SOLUTIONSTATUS.GLP_FEAS:
                    case GLP_SOLUTIONSTATUS.GLP_OPT:
                        solutionStatus = SolutionStatus.Optimal;
                        modelStatus = ModelStatus.Feasible;
                        break;
                    default:
                        solutionStatus = SolutionStatus.NoSolutionValues;
                        modelStatus = ModelStatus.Unknown;
                        break;
                }

                //Set the objective and variables' activities
                Dictionary<string, double> objValues = null;
                Dictionary<string, double> varValues = null;
                if (solutionStatus != SolutionStatus.NoSolutionValues)
                {
                    objValues = new Dictionary<string, double>();
                    if (model.ObjectivesCount != 0)
                    {
                        objValues.Add(model.Objectives.Single().Name,
                            (this.IsMixedIntegerModel ? glp_mip_obj_val(_glpk_model) : glp_get_obj_val(_glpk_model)));
                    }

                    varValues = new Dictionary<string, double>();
                    foreach (var variableKey in variablesToIdx)
                    {
                        double value = -1;

                        if (this.IsMixedIntegerModel)
                            value = glp_mip_col_val(_glpk_model, variablesToIdx[variableKey.Key]);
                        else
                            value = glp_get_col_prim(_glpk_model, variablesToIdx[variableKey.Key]);

                        varValues.Add(variableKey.Key.Name, value);
                    }

                    //// Write solution values
                    //if (Directory.Exists(sDebugDirectory))
                    //{
                    //    try
                    //    {
                    //        System.IO.StreamWriter file = new System.IO.StreamWriter(sDebugDirectory + "solution.txt");

                    //        foreach(var kvp in varValues.OrderBy(kvp => kvp.Key))
                    //            file.WriteLine(kvp.Key + "\t\t\t"+ kvp.Value);

                    //        file.Close();
                    //    } 
                    //    catch (Exception e)
                    //    {
                    //        WriteLogLine("Fehler beim Schreiben der Solverlösung im Debug-Modus:" + e.Message);
                    //    }
                    //}
                }

                // Create a new Optimization.Framework solution object and return it
                ISolution solution = new Solution(model.Name, overallWallTime, modelStatus,
                                            solutionStatus,
                                            varValues, null, objValues);

                return solution;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
            finally
            {
                IsBusy = false;
            }

            return null;
        }



        /// <summary>
        /// Solves a given linear problem using the simplex method
        /// </summary>
        /// <param name="lp">A pointer to the problem structure.</param>
        /// <param name="_ia">The row array.</param>
        /// <param name="_ja">The column array.</param>
        /// <param name="_ar">The coefficients.</param>
        /// <returns>True if solved successfully, otherwise false.</returns>
        private unsafe bool SolveProblem(double* lp, ArrayList _ia, ArrayList _ja, ArrayList _ar)
        {
            int numRows = glp_get_num_rows(lp);
            int numCols = glp_get_num_cols(lp);

            unsafe
            {
                int[] ia = (int[])_ia.ToArray(typeof(int));
                int[] ja = (int[])_ja.ToArray(typeof(int));
                double[] ar = (double[])_ar.ToArray(typeof(double));

                fixed (int* iap = ia)
                {
                    fixed (int* jap = ja)
                    {
                        fixed (double* arp = ar)
                        {
                            glp_load_matrix(lp, ia.Length - 1, iap, jap, arp);
                        }
                    }
                }

                // Hier Zeiger auf Konfigurationsstruktur GLP_SMCP übergeben um
                int res = -1;
                if (this.IsMixedIntegerModel)
                {
                    // Calculate initial basis
                    res = glp_simplex(lp, null);
                    if (res == 0)
                    {
                        //fixed (void* iocc = &this.IOCP)
                        {
                            //res = glp_intopt(lp, iocc);
                            res = glp_intopt(lp, null);
                        }
                        if (res == 0)
                            return true;
                        else
                            return false;
                    }
                    else
                        return false;
                }
                else
                    res = glp_simplex(lp, null);

                if (res == 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }


        #endregion

        /// <summary>
        /// If this solver instance is busy abort the run as soon as possible, or do nothing if this solver instance is not busy.
        /// </summary>
        /// <exception cref="System.NotSupportedException">If this solver instance not supports aborting.</exception>
        public void Abort()
        {
            throw new NotImplementedException("Abort() not supported yet.");
        }
    }
}
