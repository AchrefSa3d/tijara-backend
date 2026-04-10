require('dotenv').config();
const express = require('express');
const cors    = require('cors');

const app = express();

// ─── CORS ────────────────────────────────────────────────────
app.use(cors({
  origin: [
    process.env.FRONTEND_URL || 'http://localhost:4200',
    'http://localhost:4300',
  ],
  credentials: true,
}));

// ─── Body parser ─────────────────────────────────────────────
app.use(express.json({ limit: '10mb' }));
app.use(express.urlencoded({ extended: true, limit: '10mb' }));

// ─── Routes ──────────────────────────────────────────────────
app.use('/api/auth',         require('./routes/auth'));
app.use('/api/products',     require('./routes/products'));
app.use('/api/orders',       require('./routes/orders'));
app.use('/api/reclamations', require('./routes/reclamations'));
app.use('/api/categories',   require('./routes/categories'));
app.use('/api/annonces',     require('./routes/annonces'));
app.use('/api/admin',        require('./routes/admin'));

// ─── Health check ────────────────────────────────────────────
app.get('/api/health', (req, res) => {
  res.json({ status: 'OK', app: 'Tijara API', version: '1.0.0', timestamp: new Date() });
});

// ─── 404 ─────────────────────────────────────────────────────
app.use((req, res) => {
  res.status(404).json({ message: `Route ${req.method} ${req.path} introuvable.` });
});

// ─── Error handler ───────────────────────────────────────────
app.use((err, req, res, _next) => {
  console.error('Unhandled error:', err);
  res.status(500).json({ message: 'Erreur interne du serveur.' });
});

// ─── Démarrage ───────────────────────────────────────────────
const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
  console.log('\n╔══════════════════════════════════════════╗');
  console.log(`║  🚀 Tijara API  →  http://localhost:${PORT}   ║`);
  console.log('╚══════════════════════════════════════════╝\n');
  console.log('  Endpoints disponibles :');
  console.log('  POST  /api/auth/login');
  console.log('  POST  /api/auth/register');
  console.log('  GET   /api/auth/me');
  console.log('  GET   /api/products');
  console.log('  GET   /api/categories');
  console.log('  GET   /api/orders     (auth)');
  console.log('  POST  /api/orders     (auth)');
  console.log('  GET   /api/reclamations (auth)');
  console.log('  GET   /api/health\n');
});
