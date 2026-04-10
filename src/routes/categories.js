const router = require('express').Router();
const ctrl = require('../controllers/categoryController');
const { authenticate, authorize } = require('../middleware/auth');

router.get('/',   ctrl.getAll);
router.post('/',  authenticate, authorize('admin'), ctrl.create);

module.exports = router;
