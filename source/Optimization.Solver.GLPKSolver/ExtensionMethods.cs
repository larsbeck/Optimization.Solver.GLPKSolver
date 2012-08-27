using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Optimization.Solver.GLPK
{
    /// <summary>
    /// Extension methods related to GLPK
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// Read Mathprog model into an OptimizationFramework model
        /// </summary>
        /// <param name="model">This model</param>
        /// <param name="modFilename">.mod file name</param>
        /// <param name="datFilename">.dat file name (optional)</param>
        /// <param name="outputFilename">output file name (optional)</param>
        /// <returns>OptimizationFramework model, filled with model data from MathProg</returns>
        public static void Load(this Model model, string modFilename, string datFilename = null, string outputFilename = null)
        {
            SetupHelpers.SetPathForSolverLib("GLPK");

            bool useTempOutputFile;
            int objIndex1 = 0;

            // Check if files exist
            if (!File.Exists(modFilename))
                throw new ApplicationException(string.Format("MOD-File '{0}' does not exist.", modFilename));

            if (!string.IsNullOrEmpty(datFilename) && !File.Exists(datFilename))
                throw new ApplicationException(string.Format("DAT-File '{0}' does not exist.", datFilename));

            if (outputFilename != null && outputFilename == "")
                throw new ApplicationException(string.Format("Output file name may not be an empty string"));

            // Create temp file if necessary
            if (string.IsNullOrEmpty(outputFilename))
            {
                outputFilename = Path.GetTempFileName();
                useTempOutputFile = true;
            }
            else
                useTempOutputFile = false;

            // Read model
            IntPtr lpx;
            try
            {
                // Try if it's a modified GLPK.DLL
                lpx = NativeFunctions._glp_lpx_read_model2(modFilename, datFilename, outputFilename);
            }
            catch (Exception)
            {
                // Else it must be normal GLPK.DLL
                lpx = NativeFunctions._glp_lpx_read_model(modFilename, datFilename, outputFilename);
            }

            // Read output
            string messageBuffer;
            try
            {
                using (var sr = new StreamReader(outputFilename))
                {
                    messageBuffer = sr.ReadToEnd();
                }

                // Delte temp output file
                if (useTempOutputFile)
                    File.Delete(outputFilename);
            }
            catch (Exception e)
            {
                messageBuffer = "Could not read Output from GLPK MathProg. " + e.Message;
            }

            if (lpx.ToInt64() == 0)
                throw new ApplicationException(messageBuffer);

            // Get model class and dimensions
            int modelclass = NativeFunctions._glp_lpx_get_class(lpx);
            int nCols = NativeFunctions._glp_lpx_get_num_cols(lpx);
            int nRows = NativeFunctions._glp_lpx_get_num_rows(lpx);

            // Auxiliary lists for storing index-object relations
            var varIndices = new SortedList<int, Variable>(nCols);
            var conIndices = new SortedList<int, Constraint>(nRows);

            // Set optimization direction and name
            IntPtr ptrObjName = NativeFunctions._glp_lpx_get_obj_name(lpx);
            string objName = Marshal.PtrToStringAnsi(ptrObjName);

            var objectiveConstant = new ConstantExpression(0);
            Objective obj = NativeFunctions._glp_lpx_get_obj_dir(lpx) == NativeFunctions.LPX_MI ? new Objective(objectiveConstant, objName) : new Objective(objectiveConstant, objName, ObjectiveSense.Maximize);

            var objectiveSumExpressionBuilder = new SumExpressionBuilder();

            // Read Columns
            for (int i = 1; i <= nCols; i++)
            {
                IntPtr ptr = NativeFunctions._glp_lpx_get_col_name(lpx, i);
                string name = Marshal.PtrToStringAnsi(ptr);
                double cost = NativeFunctions._glp_lpx_get_obj_coef(lpx, i);
                int type = NativeFunctions._glp_lpx_get_col_type(lpx, i);

                double lb = double.NegativeInfinity;
                if (type == NativeFunctions.LPX_LO || type == NativeFunctions.LPX_DB || type == NativeFunctions.LPX_FX)
                    lb = NativeFunctions._glp_lpx_get_col_lb(lpx, i);

                double ub = double.PositiveInfinity;
                if (type == NativeFunctions.LPX_UP || type == NativeFunctions.LPX_DB || type == NativeFunctions.LPX_FX)
                    ub = NativeFunctions._glp_lpx_get_col_ub(lpx, i);

                VariableType vtype;
                if (modelclass == NativeFunctions.LPX_LP)
                    vtype = VariableType.Continuous;
                else if (NativeFunctions._glp_lpx_get_col_kind(lpx, i) == NativeFunctions.LPX_CV)
                    vtype = VariableType.Continuous;
                else
                    vtype = VariableType.Integer;

                var var = new Variable(name, lb, ub, vtype);   // Store variable
                model.AddVariable(var);

                objectiveSumExpressionBuilder.Add(var * cost);            // Store objective

                varIndices.Add(i, var); // Remember index-variable relation
            }
            model.AddObjective(objectiveSumExpressionBuilder.ToExpression());

            // Read Rows
            for (int i = 1; i <= nRows; i++)
            {
                int type = NativeFunctions._glp_lpx_get_row_type(lpx, i);

                if (type == NativeFunctions.LPX_FR && objIndex1 == 0)
                    objIndex1 = i;
                else
                {
                    IntPtr ptr = NativeFunctions._glp_lpx_get_row_name(lpx, i);
                    string name = Marshal.PtrToStringAnsi(ptr);

                    double lb = double.NegativeInfinity;
                    if (type == NativeFunctions.LPX_LO || type == NativeFunctions.LPX_DB || type == NativeFunctions.LPX_FX)
                        lb = NativeFunctions._glp_lpx_get_row_lb(lpx, i);

                    double ub = double.PositiveInfinity;
                    if (type == NativeFunctions.LPX_UP || type == NativeFunctions.LPX_DB || type == NativeFunctions.LPX_FX)
                        ub = NativeFunctions._glp_lpx_get_row_ub(lpx, i);

                    var con = new Constraint(null, name, lb, ub);    // Store row
                    model.AddConstraint(con);

                    conIndices.Add(i, con); // Remember index-constraint relation
                }
            }

            // Read Nonzeros rowwise
            int[] colindices = new int[nCols + 1];
            GCHandle colindices_pin = GCHandle.Alloc(colindices, GCHandleType.Pinned);
            double[] nzs = new double[nCols + 1];
            GCHandle nzs_pin = GCHandle.Alloc(nzs, GCHandleType.Pinned);

            for (int i = 1; i <= nRows; i++)
            {
                // Don' read objective row
                if (i == objIndex1)
                    continue;

                int n = NativeFunctions._glp_lpx_get_mat_row(lpx, i, colindices_pin.AddrOfPinnedObject(), nzs_pin.AddrOfPinnedObject());

                Constraint con = conIndices[i];
                var conSumExpressionBuilder = new SumExpressionBuilder();

                // Store nonzeros
                for (int j = 1; j <= n; j++)
                {
                    int coli = colindices[j];
                    double nz = nzs[j];
                    Variable var = varIndices[coli];

                    conSumExpressionBuilder.Add(var * nz);
                }
                con.Expression=conSumExpressionBuilder.ToExpression();
            }


            // Release pinned arrays
            colindices_pin.Free();
            nzs_pin.Free();

            NativeFunctions._glp_lpx_delete_prob(lpx);
        }

    }
}
