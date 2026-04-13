const express = require('express');
const router  = express.Router({ mergeParams: true }); // /api/products/:id/reviews
const { authenticate } = require('../middleware/auth');
const { getProductReviews, addReview, deleteReview } = require('../controllers/reviewController');

router.get('/',              getProductReviews);
router.post('/',             authenticate, addReview);
router.delete('/:reviewId',  authenticate, deleteReview);

module.exports = router;
