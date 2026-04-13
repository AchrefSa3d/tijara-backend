const router = require('express').Router();
const { login, register, me, googleAuth } = require('../controllers/authController');
const { authenticate } = require('../middleware/auth');

router.post('/login',    login);
router.post('/register', register);
router.post('/google',   googleAuth);
router.get('/me',        authenticate, me);

module.exports = router;
