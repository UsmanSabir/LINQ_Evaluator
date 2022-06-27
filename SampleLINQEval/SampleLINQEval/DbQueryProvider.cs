//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Data.Common;
//using System.Linq;
//using System.Linq.Expressions;
//using System.Reflection;
//using System.Text;
//using System.Threading.Tasks;

//namespace SampleLINQEval
//{
//    public abstract class ProjectionRow
//    {

//        public abstract object GetValue(int index);

//    }
//    internal class ColumnProjection
//    {

//        internal string Columns;

//        internal Expression Selector;

//    }

//    internal class ColumnProjector : ExpressionVisitor
//    {

//        StringBuilder sb;

//        int iColumn;

//        ParameterExpression row;

//        static MethodInfo miGetValue;

//        internal ColumnProjector()
//        {

//            if (miGetValue == null)
//            {

//                miGetValue = typeof(ProjectionRow).GetMethod("GetValue");

//            }

//        }

//        internal ColumnProjection ProjectColumns(Expression expression, ParameterExpression row)
//        {

//            this.sb = new StringBuilder();

//            this.row = row;

//            Expression selector = this.Visit(expression);

//            return new ColumnProjection { Columns = this.sb.ToString(), Selector = selector };

//        }
        
//        protected override  Expression VisitMemberAccess(MemberExpression m)
//        {

//            if (m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter)
//            {

//                if (this.sb.Length > 0)
//                {

//                    this.sb.Append(", ");

//                }

//                this.sb.Append(m.Member.Name);

//                return Expression.Convert(Expression.Call(this.row, miGetValue, Expression.Constant(iColumn++)), m.Type);

//            }

//            else
//            {

//                return base.VisitMemberAccess(m);

//            }

//        }

//    }
//    internal class TranslateResult
//    {

//        internal string CommandText;

//        internal LambdaExpression Prsojector;

//    }
//    internal class QueryTranslator : ExpressionVisitor
//    {

//        StringBuilder sb;

//        ParameterExpression row;

//        ColumnProjection projection;

//        internal QueryTranslator()
//        {

//        }

//        internal TranslateResult Translate(Expression expression)
//        {

//            this.sb = new StringBuilder();

//            this.row = Expression.Parameter(typeof(ProjectionRow), "row");

//            this.Visit(expression);

//            return new TranslateResult
//            {

//                CommandText = this.sb.ToString(),

//                Projector = this.projection != null ? Expression.Lambda(this.projection.Selector, this.row) : null

//            };

//        }

//        protected override Expression VisitMethodCall(MethodCallExpression m)
//        {

//            if (m.Method.DeclaringType == typeof(Queryable))
//            {

//                if (m.Method.Name == "Where")
//                {

//                    sb.Append("SELECT * FROM (");

//                    this.Visit(m.Arguments[0]);

//                    sb.Append(") AS T WHERE ");

//                    LambdaExpression lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);

//                    this.Visit(lambda.Body);

//                    return m;

//                }

//                else if (m.Method.Name == "Select")
//                {

//                    LambdaExpression lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);

//                    ColumnProjection projection = new ColumnProjector().ProjectColumns(lambda.Body, this.row);

//                    sb.Append("SELECT ");

//                    sb.Append(projection.Columns);

//                    sb.Append(" FROM (");

//                    this.Visit(m.Arguments[0]);

//                    sb.Append(") AS T ");

//                    this.projection = projection;

//                    return m;

//                }

//            }

//            throw new NotSupportedException(string.Format("The method '{0}' is not supported", m.Method.Name));

//        }
         
//}

//    internal class ProjectionReader<T> : IEnumerable<T>, IEnumerable
//    {

//        Enumerator enumerator;

//        internal ProjectionReader(DbDataReader reader, Func<ProjectionRow, T> projector)
//        {

//            this.enumerator = new Enumerator(reader, projector);

//        }

//        public IEnumerator<T> GetEnumerator()
//        {

//            Enumerator e = this.enumerator;

//            if (e == null)
//            {

//                throw new InvalidOperationException("Cannot enumerate more than once");

//            }

//            this.enumerator = null;

//            return e;

//        }

//        IEnumerator IEnumerable.GetEnumerator()
//        {

//            return this.GetEnumerator();

//        }

//        class Enumerator : ProjectionRow, IEnumerator<T>, IEnumerator, IDisposable
//        {

//            DbDataReader reader;

//            T current;

//            Func<ProjectionRow, T> projector;

//            internal Enumerator(DbDataReader reader, Func<ProjectionRow, T> projector)
//            {

//                this.reader = reader;

//                this.projector = projector;

//            }

//            public override object GetValue(int index)
//            {

//                if (index >= 0)
//                {

//                    if (this.reader.IsDBNull(index))
//                    {

//                        return null;

//                    }

//                    else
//                    {

//                        return this.reader.GetValue(index);

//                    }

//                }

//                throw new IndexOutOfRangeException();

//            }

//            public T Current
//            {

//                get { return this.current; }

//            }

//            object IEnumerator.Current
//            {

//                get { return this.current; }

//            }

//            public bool MoveNext()
//            {

//                if (this.reader.Read())
//                {

//                    this.current = this.projector(this);

//                    return true;

//                }

//                return false;

//            }

//            public void Reset()
//            {

//            }

//            public void Dispose()
//            {

//                this.reader.Dispose();

//            }

//        }

//    }

//    public class DbQueryProvider : QueryProvider
//    {

//        DbConnection connection;

//        public DbQueryProvider(DbConnection connection)
//        {

//            this.connection = connection;

//        }

//        public override string GetQueryText(Expression expression)
//        {

//            return this.Translate(expression).CommandText;

//        }

//        public override object Execute(Expression expression)
//        {

//            TranslateResult result = this.Translate(expression);

//            DbCommand cmd = this.connection.CreateCommand();

//            cmd.CommandText = result.CommandText;

//            DbDataReader reader = cmd.ExecuteReader();

//            Type elementType = TypeSystem.GetElementType(expression.Type);

//            if (result.Projector != null)
//            {

//                Delegate projector = result.Projector.Compile();

//                return Activator.CreateInstance(

//                    typeof(ProjectionReader<>).MakeGenericType(elementType),

//                    BindingFlags.Instance | BindingFlags.NonPublic, null,

//                    new object[] { reader, projector },

//                    null

//                    );

//            }

//            else
//            {

//                return Activator.CreateInstance(

//                    typeof(ObjectReader<>).MakeGenericType(elementType),

//                    BindingFlags.Instance | BindingFlags.NonPublic, null,

//                    new object[] { reader },

//                    null

//                    );

//            }

//        }

//        private TranslateResult Translate(Expression expression)
//        {

//            expression = Evaluator.PartialEval(expression);

//            return new QueryTranslator().Translate(expression);

//        }

//    }

//    //public class DbQueryProvider : QueryProvider
//    //{

//    //private string Translate(Expression expression)
//    //    {

//    //        expression = Evaluator.PartialEval(expression);

//    //        return new QueryTranslator().Translate(expression);

//    //    }

//    //}
//}
