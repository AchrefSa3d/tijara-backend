const router = require('express').Router();
const ctrl = require('../controllers/orderController');
const { authenticate, authorize } = require('../middleware/auth');

router.get('/',              authenticate, ctrl.getAll);
router.get('/:id',           authenticate, ctrl.getOne);
router.post('/',             authenticate, authorize('user'), ctrl.create);
router.patch('/:id/status',  authenticate, authorize('admin','vendor'), ctrl.updateStatus);

module.exports = router;
