const router = require('express').Router();
const ctrl = require('../controllers/reclamationController');
const { authenticate, authorize } = require('../middleware/auth');

router.get('/',          authenticate, ctrl.getAll);
router.post('/',         authenticate, ctrl.create);
router.patch('/:id',     authenticate, authorize('admin'), ctrl.reply);

module.exports = router;
