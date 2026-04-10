const bcrypt = require('bcryptjs');
const jwt    = require('jsonwebtoken');
const { query } = require('../config/database');

// ─── POST /api/auth/login ─────────────────────────────────────
async function login(req, res) {
  const { email, password } = req.body;
  if (!email || !password)
    return res.status(400).json({ message: 'Email et mot de passe requis.' });

  try {
    const result = await query(
      'SELECT * FROM users WHERE email = @email AND is_active = 1',
      { email: email.toLowerCase() }
    );
    const rows = result.recordset;

    if (!rows.length)
      return res.status(401).json({ message: 'Email ou mot de passe incorrect.' });

    const user = rows[0];
    const valid = await bcrypt.compare(password, user.password_hash);
    if (!valid)
      return res.status(401).json({ message: 'Email ou mot de passe incorrect.' });

    // Vérifier approbation pour les vendeurs
    if (user.role === 'vendor' && !user.is_approved) {
      return res.status(403).json({
        message: 'Votre compte vendeur est en attente de validation par l\'administrateur.',
        status: 'pending_approval'
      });
    }

    const token = jwt.sign(
      { id: user.id, email: user.email, role: user.role,
        firstName: user.first_name, lastName: user.last_name },
      process.env.JWT_SECRET,
      { expiresIn: process.env.JWT_EXPIRES_IN || '7d' }
    );

    res.json({
      token,
      user: {
        id:         user.id,
        email:      user.email,
        role:       user.role,
        firstName:  user.first_name,
        lastName:   user.last_name,
        phone:      user.phone,
        city:       user.city,
        isApproved: user.is_approved,
      }
    });
  } catch (err) {
    console.error('login:', err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── POST /api/auth/register ──────────────────────────────────
async function register(req, res) {
  const { email, password, firstName, lastName, phone, city, role = 'user', shopName, companyNumber } = req.body;
  if (!email || !password)
    return res.status(400).json({ message: 'Email et mot de passe requis.' });
  if (!firstName || !lastName)
    return res.status(400).json({ message: 'Prénom et nom requis.' });
  if (role === 'vendor' && !shopName)
    return res.status(400).json({ message: 'Le nom de la boutique est requis pour les vendeurs.' });

  const userRole = ['user', 'vendor'].includes(role) ? role : 'user';
  const isApproved = userRole === 'vendor' ? 0 : 1;

  try {
    const exists = await query(
      'SELECT id FROM users WHERE email = @email',
      { email: email.toLowerCase() }
    );
    if (exists.recordset.length > 0)
      return res.status(409).json({ message: 'Cet email est déjà utilisé.' });

    const hash = await bcrypt.hash(password, 10);
    const result = await query(
      `INSERT INTO users (email, password_hash, role, first_name, last_name, phone, city, is_approved, shop_name, company_number)
       OUTPUT INSERTED.id, INSERTED.email, INSERTED.role, INSERTED.first_name, INSERTED.last_name, INSERTED.is_approved, INSERTED.shop_name
       VALUES (@email, @hash, @role, @firstName, @lastName, @phone, @city, @isApproved, @shopName, @companyNumber)`,
      {
        email: email.toLowerCase(), hash, role: userRole,
        firstName, lastName,
        phone: phone || null, city: city || null,
        isApproved,
        shopName: shopName || null,
        companyNumber: companyNumber || null
      }
    );

    const user = result.recordset[0];

    if (userRole === 'vendor') {
      return res.status(201).json({
        message: 'Compte vendeur créé. En attente de validation par l\'administrateur.',
        status: 'pending_approval',
        user: { id: user.id, email: user.email, role: user.role }
      });
    }

    const token = jwt.sign(
      { id: user.id, email: user.email, role: user.role },
      process.env.JWT_SECRET,
      { expiresIn: process.env.JWT_EXPIRES_IN || '7d' }
    );
    res.status(201).json({ token, user });
  } catch (err) {
    console.error('register:', err);
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

// ─── GET /api/auth/me ─────────────────────────────────────────
async function me(req, res) {
  try {
    const result = await query(
      'SELECT id, email, role, first_name, last_name, phone, city, is_approved, created_at FROM users WHERE id = @id',
      { id: req.user.id }
    );
    if (!result.recordset.length)
      return res.status(404).json({ message: 'Utilisateur introuvable.' });
    res.json(result.recordset[0]);
  } catch (err) {
    res.status(500).json({ message: 'Erreur serveur.' });
  }
}

module.exports = { login, register, me };
