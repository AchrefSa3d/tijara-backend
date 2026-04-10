const sql = require('mssql');
require('dotenv').config();

const config = {
  server:   process.env.DB_HOST || 'localhost',
  port:     parseInt(process.env.DB_PORT) || 1433,
  database: process.env.DB_NAME || 'tijara_db',
  user:     process.env.DB_USER || 'sa',
  password: process.env.DB_PASSWORD || 'Tijara2026!',
  options: {
    trustServerCertificate: true,
    enableArithAbort: true,
  },
  pool: {
    max: 10,
    min: 0,
    idleTimeoutMillis: 30000
  }
};

let pool;

async function getPool() {
  if (!pool) {
    try {
      pool = await sql.connect(config);
      console.log('✅ Connecté à SQL Server (tijara_db)');
    } catch (err) {
      console.error('❌ Erreur connexion SQL Server :', err.message);
      throw err;
    }
  }
  return pool;
}

async function query(queryString, params = {}) {
  const p = await getPool();
  const request = p.request();
  for (const [key, value] of Object.entries(params)) {
    request.input(key, value);
  }
  return request.query(queryString);
}

module.exports = { getPool, query, sql };
