using System;
using System.Collections.Generic;
using System.Text;
using System.Data.Common;
using System.Data;
using System.Configuration;
using System.Web;

namespace Simon.Common
{
    /// <summary>
    /// 通用数据库操作类
    /// </summary>
    public class SimonDB
    {
        static string DBConnString = ConfigurationManager.ConnectionStrings["DBConn"].ConnectionString;
        static string DBConnProviderName = ConfigurationManager.ConnectionStrings["DBConn"].ProviderName;
        static DbProviderFactory providerFactory = DbProviderFactories.GetFactory(DBConnProviderName);

        /// <summary>
        /// 创建DbParameter (ParameterDirection.Input) 类型
        /// </summary>
        /// <param name="parName">参数名称</param>
        /// <param name="parValue">参数值</param>
        /// <returns></returns>
        public static DbParameter CreDbPar(string parName, object parValue)
        {
            return CreDbPar(parName, ParameterDirection.Input, parValue);
        }

        /// <summary>
        /// 创建DbParameter (在存储过程中：使用ParameterDirection.Input、ParameterDirection.InputOutput类型参数时，注意parValue值类型需要和存储过程中参数类型相符)
        /// </summary>
        /// <param name="parName">参数名称</param>
        /// <param name="parDirection">参数类型</param>
        /// <param name="parValue">参数值</param>
        /// <returns></returns>
        public static DbParameter CreDbPar(string parName, ParameterDirection parDirection, object parValue)
        {
            DbParameter parameter = providerFactory.CreateParameter();
            parameter.ParameterName = parName;
            parameter.Direction = parDirection;
            parameter.Value = parValue;
            return parameter;
        }

        /// <summary>
        /// 初始化Dbconnection对象
        /// </summary>
        /// <returns></returns>
        private static DbConnection BuildConnection()
        {
            DbConnection Connection = providerFactory.CreateConnection();
            Connection.ConnectionString = DBConnString;
            return Connection;
        }

        /// <summary>
        /// 配置Command
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="conn"></param>
        /// <param name="trans"></param>
        /// <param name="cmdText"></param>
        /// <param name="parms"></param>
        private static void PrepareCommand(DbCommand cmd, DbConnection conn, DbTransaction trans, string cmdText, string commandType, DbParameter[] parms)
        {
            if (conn.State != ConnectionState.Open)
                conn.Open();
            cmd.Connection = conn;
            cmd.CommandText = cmdText;
            if (trans != null)
                cmd.Transaction = trans;
            if (commandType.ToLower() == "sp")
                cmd.CommandType = CommandType.StoredProcedure; //cmdType;
            else
                cmd.CommandType = CommandType.Text;//cmdType;
            if (parms != null)
            {
                cmd.Parameters.AddRange(parms);
            }
        }

        /// <summary>
        /// 执行SQL语句,返回受影响的行数
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <returns>受影响的行数</returns>
        public static int ExecuteNonQuery(string commandText)
        {
            return ExecuteNonQuery(commandText, "", null);
        }

        /// <summary>
        /// 执行带数的SQL语句,返回受影响的行数
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <returns>受影响的行数</returns>
        public static int ExecuteNonQuery(string commandText, DbParameter[] parms)
        {
            return ExecuteNonQuery(commandText, "", parms);
        }

        /// <summary>
        /// 执行带数的SQL语句,返回受影响的行数
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandType">commandtype:sp为存储过程,其他为text SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <returns>受影响的行数</returns>
        public static int ExecuteNonQuery(string commandText, string commandType, DbParameter[] parms)
        {
            int flagValue;
            using (DbConnection connection = BuildConnection())
            {
                using (DbCommand command = providerFactory.CreateCommand())
                {
                    try
                    {
                        PrepareCommand(command, connection, null, commandText, commandType, parms);
                        flagValue = command.ExecuteNonQuery();
                        command.Parameters.Clear();
                    }
                    catch (DbException exception)
                    {
                        connection.Close();
                        throw new Exception(exception.Message);
                    }
                    finally
                    {
                        if (command != null)
                            command.Dispose();
                    }
                }
            }
            return flagValue;
        }

        /// <summary>
        /// 执行存储过程，获取存储过程返回值
        /// </summary>
        /// <param name="storedProcName">存储过程名</param>
        /// <param name="parms">存储过程参数</param>
        /// <returns>存储过程返回值 ReturnValue</returns>
        public static int RunProcedure(string storedProcName, DbParameter[] parms)
        {
            int result;
            using (DbConnection connection = BuildConnection())
            {
                using (DbCommand command = providerFactory.CreateCommand())
                {
                    try
                    {
                        PrepareCommand(command, connection, null, storedProcName, "sp", parms);
                        command.Parameters.Add(CreDbPar("@ReturnValue", ParameterDirection.ReturnValue, null));
                        command.ExecuteNonQuery();
                        result = (int)command.Parameters["@ReturnValue"].Value;
                        command.Parameters.Clear();
                    }
                    catch (DbException exception)
                    {
                        connection.Close();
                        throw new Exception(exception.Message);
                    }
                    finally
                    {
                        if (command != null) command.Dispose();
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 执行SQL语句,返回第一行第一列
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <returns>第一行第一列</returns>
        public static Object ExecuteScalar(string commandText)
        {
            return ExecuteScalar(commandText, "", null);
        }

        /// <summary>
        /// 执行带参数的SQL语句,返回第一行第一列
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <returns>第一行第一列</returns>
        public static object ExecuteScalar(string commandText, DbParameter[] parms)
        {
            return ExecuteScalar(commandText, "", parms);
        }

        /// <summary>
        /// 执行带参数的SQL语句,返回第一行第一列
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandType">commandtype:sp为存储过程,其他为text SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <returns>第一行第一列</returns>
        public static object ExecuteScalar(string commandText, string commandType, DbParameter[] parms)
        {
            object flagValue;
            using (DbConnection connection = BuildConnection())
            {
                using (DbCommand command = providerFactory.CreateCommand())
                {
                    try
                    {
                        PrepareCommand(command, connection, null, commandText, commandType, parms);
                        object objA = command.ExecuteScalar();
                        command.Parameters.Clear();
                        if ((object.Equals(objA, null)) || (object.Equals(objA, DBNull.Value)))
                        {
                            return null;
                        }
                        flagValue = objA;
                    }
                    catch (DbException exception)
                    {
                        connection.Close();
                        throw new Exception(exception.Message);
                    }
                    finally
                    {
                        if (command != null)
                            command.Dispose();
                    }
                }
            }
            return flagValue;
        }

        /// <summary>
        /// 执行SQL查询语句,返回是否存在重复记录
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <returns>bool值,指示是否存在重复值</returns>
        public static bool IsExist(string commandText)
        {
            return (DataTable(commandText).Rows.Count > 0);
        }

        /// <summary>
        /// 执行SQL查询(Select)语句,返回是否存在重复记录
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <returns>bool值,指示是否存在重复值</returns>
        public static bool IsExist(string commandText, DbParameter[] parms)
        {
            return (DataTable(commandText, parms).Rows.Count > 0);
        }

        /// <summary>
        /// 执行SQL查询(Select)语句,返回是否存在重复记录
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandType">commandtype:sp为存储过程,其他为text SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <returns>bool值,指示是否存在重复值</returns>
        public static bool IsExist(string commandText, string commandType, DbParameter[] parms)
        {
            return (DataTable(commandText, commandType, parms).Rows.Count > 0);
        }

        /// <summary>
        /// 执行SQL插入(Insert)语句,返回新增记录自增量ID
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <returns>新增记录自增量ID</returns>
        public static int Insert(string commandText)
        {
            return Insert(commandText, "", null);
        }

        /// <summary>
        /// 执行SQL插入(Insert)语句,返回新增记录自增量ID
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <returns>新增记录自增量ID</returns>
        public static int Insert(string commandText, DbParameter[] parms)
        {
            return Insert(commandText, "", parms);
        }

        /// <summary>
        /// 执行SQL插入(Insert)语句,返回新增记录自增量ID
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandType">commandtype:sp为存储过程,其他为text SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <returns>新增记录自增量ID</returns>
        public static int Insert(string commandText, string commandType, DbParameter[] parms)
        {
            int flagValue;
            using (DbConnection connection = BuildConnection())
            {
                using (DbCommand command = providerFactory.CreateCommand())
                {
                    try
                    {
                        PrepareCommand(command, connection, null, commandText, commandType, parms);
                        command.ExecuteNonQuery();
                        command.CommandText = "SELECT @@IDENTITY";
                        flagValue = Convert.ToInt32(command.ExecuteScalar());
                        //flagValue = (int)command.ExecuteScalar();  //这个隐性转换的写法Access数据库有效，Sql2005无效
                        command.Parameters.Clear();
                    }
                    catch (DbException exception)
                    {
                        connection.Close();
                        throw new Exception(exception.Message);
                    }
                    finally
                    {
                        if (command != null)
                            command.Dispose();
                    }
                }
            }
            return flagValue;
        }

        /// <summary>
        /// 执行SQL语句,返回DataSet;
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="tableName">表名</param>
        /// <returns>DataSet</returns>
        public static DataSet DataSet(string commandText, string tableName)
        {
            return DataSet(commandText, "", null, tableName);
        }

        /// <summary>
        /// 执行带参数的SQL语句,返回DataSet;
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <param name="tableName">表名</param>
        /// <returns>DataSet</returns>
        public static DataSet DataSet(string commandText, DbParameter[] parms, string tableName)
        {
            return DataSet(commandText, "", parms, tableName);
        }

        /// <summary>
        /// 执行带参数的SQL语句,返回DataSet;
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandType">commandtype:sp为存储过程,其他为text SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <param name="tableName">表名</param>
        /// <returns>DataSet</returns>
        public static DataSet DataSet(string commandText, string commandType, DbParameter[] parms, string tableName)
        {
            DataSet dataSet = new DataSet();
            using (DbConnection connection = BuildConnection())
            {
                using (DbCommand command = providerFactory.CreateCommand())
                {
                    try
                    {
                        DbDataAdapter Adapter = providerFactory.CreateDataAdapter();
                        PrepareCommand(command, connection, null, commandText, commandType, parms);
                        Adapter.SelectCommand = command;
                        Adapter.Fill(dataSet, tableName);
                        command.Parameters.Clear();
                    }
                    catch (DbException exception)
                    {
                        connection.Close();
                        throw new Exception(exception.Message);
                    }
                    finally
                    {
                        if (command != null)
                            command.Dispose();
                    }
                }
            }
            return dataSet;
        }

        /// 执行SQL语句,返回DataReader;
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <returns>DataReader</returns>
        public static DbDataReader DataReader(string commandText)
        {
            return DataReader(commandText, "", null);
        }

        /// 执行带参数的SQL语句,返回DataReader;
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <returns>DataReader</returns>
        public static DbDataReader DataReader(string commandText, DbParameter[] parms)
        {
            return DataReader(commandText, "", parms);
        }

        /// <summary>
        /// 执行带参数的SQL语句,返回DataReader;
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandType">commandtype:sp为存储过程,其他为text SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <returns>DataReader</returns>
        public static DbDataReader DataReader(string commandText, string commandType, DbParameter[] parms)
        {
            DbConnection connection = BuildConnection();
            DbCommand command = providerFactory.CreateCommand();
            try
            {
                PrepareCommand(command, connection, null, commandText, commandType, parms);
                DbDataReader myDataReader = command.ExecuteReader(CommandBehavior.CloseConnection);
                command.Parameters.Clear();
                return myDataReader;
            }
            catch (DbException exception)
            {
                connection.Close();
                throw new Exception(exception.Message);
            }
        }

        /// <summary>
        /// 执行SQL语句,返回DataTable;
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <returns>DataTable</returns>
        public static DataTable DataTable(string commandText)
        {
            return DataSet(commandText, "thistable").Tables["thistable"];
        }

        /// <summary>
        /// 执行带参数的SQL语句,返回DataTable;
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <returns>DataTable</returns>
        public static DataTable DataTable(string commandText, DbParameter[] parms)
        {
            return DataSet(commandText, parms, "thistable").Tables["thistable"];
        }

        /// <summary>
        /// 执行带参数的SQL语句,返回DataTable;
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandType">commandtype:sp为存储过程,其他为text SQL语句</param>
        /// <param name="parms">DbParameter</param>
        /// <returns>DataTable</returns>
        public static DataTable DataTable(string commandText, string commandType, DbParameter[] parms)
        {
            return DataSet(commandText, commandType, parms, "thistable").Tables["thistable"];
        }

        /// <summary>
        /// 获取DataTable前几条数据
        /// </summary>
        /// <param name="sourceDT">源DataTable</param>
        /// <param name="TopN">前N条数据</param>
        /// <returns></returns>
        public static DataTable DataTableSelectTopN(DataTable sourceDT, int TopN)
        {
            if (sourceDT.Rows.Count < TopN) return sourceDT;

            DataTable NewTable = sourceDT.Clone();
            DataRow[] rows = sourceDT.Select("1=1");
            for (int i = 0; i < TopN; i++)
            {
                NewTable.ImportRow((DataRow)rows[i]);
            }
            return NewTable;
        }

        /// <summary>
        /// DataTable筛选，排序返回符合条件行
        /// eg:SortExprDataTable(dt,"Sex='男'","Time Desc")
        /// </summary>
        /// <param name="sourceDT">传入的DataTable</param>
        /// <param name="strExpr">筛选条件</param>
        /// <param name="strSort">排序条件</param>
        public static DataTable SortDataTable(DataTable sourceDT, string strExpr, string strSort)
        {
            if (strExpr != string.Empty) sourceDT.DefaultView.RowFilter = strExpr;
            if (strSort != string.Empty) sourceDT.DefaultView.Sort = strSort;
            return sourceDT;
        }

    }
}
