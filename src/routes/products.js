const router = require('express').Router();
const ctrl = require('../controllers/productController');
const reviewRouter = require('./reviews');
const { authenticate, authorize } = require('../middleware/auth');

router.get('/',               ctrl.getAll);
router.get('/mine',           authenticate, authorize('vendor','admin'), ctrl.getMyProducts);
router.get('/vendor/:vendorId', ctrl.getVendorProfile);
router.get('/:id',   ctrl.getOne);
router.post('/',    authenticate, authorize('vendor','admin'), ctrl.create);
router.put('/:id',  authenticate, authorize('vendor','admin'), ctrl.update);
router.delete('/:id', authenticate, authorize('vendor','admin'), ctrl.remove);

// Nested: /api/products/:id/reviews
router.use('/:id/reviews', reviewRouter);

module.exports = router;
