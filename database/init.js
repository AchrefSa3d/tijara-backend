/**
 * TIJARA – Initialisation de la base de données
 * Crée la DB tijara_db puis applique le schéma + les données
 */
const { Client } = require('pg');
const fs  = require('fs');
const path = require('path');
require('dotenv').config({ path: path.join(__dirname, '../.env') });

const dbName = process.env.DB_NAME || 'tijara_db';

async function run() {
  // 1) Connexion à postgres (DB admin) pour créer tijara_db
  const admin = new Client({
    host:     process.env.DB_HOST,
    port:     parseInt(process.env.DB_PORT),
    database: 'postgres',
    user:     process.env.DB_USER,
    password: process.env.DB_PASSWORD,
  });

  try {
    await admin.connect();
    const res = await admin.query(
      `SELECT 1 FROM pg_database WHERE datname = $1`, [dbName]
    );

    if (res.rowCount === 0) {
      await admin.query(`CREATE DATABASE ${dbName}`);
      console.log(`✅ Base de données "${dbName}" créée.`);
    } else {
      console.log(`ℹ️  Base de données "${dbName}" déjà existante.`);
    }
  } finally {
    await admin.end();
  }

  // 2) Connexion à tijara_db pour appliquer le schéma
  const db = new Client({
    host:     process.env.DB_HOST,
    port:     parseInt(process.env.DB_PORT),
    database: dbName,
    user:     process.env.DB_USER,
    password: process.env.DB_PASSWORD,
  });

  try {
    await db.connect();

    const schema = fs.readFileSync(path.join(__dirname, 'schema.sql'), 'utf8');
    await db.query(schema);
    console.log('✅ Schéma appliqué (tables créées).');

    const seed = fs.readFileSync(path.join(__dirname, 'seed.sql'), 'utf8');
    await db.query(seed);
    console.log('✅ Données de démonstration insérées.');

    console.log('\n🚀 Base de données Tijara prête !\n');
    console.log('   Comptes de démonstration :');
    console.log('   ┌─────────────────────────┬─────────────┬─────────┐');
    console.log('   │ Email                   │ Mot de passe│ Rôle    │');
    console.log('   ├─────────────────────────┼─────────────┼─────────┤');
    console.log('   │ admin@tijara.tn          │ Admin123    │ Admin   │');
    console.log('   │ vendor@tijara.tn         │ Vendor123   │ Vendeur │');
    console.log('   │ user@tijara.tn           │ User123     │ Client  │');
    console.log('   └─────────────────────────┴─────────────┴─────────┘');
  } finally {
    await db.end();
  }
}

run().catch(err => {
  console.error('❌ Erreur init DB :', err.message);
  process.exit(1);
});
