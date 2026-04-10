const router = require('express').Router();
const ctrl   = require('../controllers/annonceController');
const { authenticate } = require('../middleware/auth');

router.get('/',                    ctrl.getApproved);
router.get('/mine',                authenticate, ctrl.getMine);
router.post('/',                   authenticate, ctrl.create);
router.delete('/:id',              authenticate, ctrl.remove);
router.post('/:id/like',           authenticate, ctrl.toggleLike);
router.get('/:id/comments',        ctrl.getComments);
router.post('/:id/comments',       authenticate, ctrl.addComment);

module.exports = router;
