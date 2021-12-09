; License
; Copyright (c) 2016-2021 Llewellyn Pritchard
; All rights reserved.
; This source code is subject to terms and conditions of the BSD License.

(library (editor)
  (export
    view
    open-views
    make-uppercase)
  (import 
    (ironscheme)
    (ironscheme clr internal))

  (define-syntax clr-using
    (lambda (e)
      (syntax-case e ()
        [(_ namespace)
         #'(define using (clr-using-internal 'namespace))])))

  (define-syntax clr-call
    (lambda (e)
      (syntax-case e ()
        [(_ type member instance args ...)
         #'(clr-call-internal 'type 'member instance args ...)])))

  (clr-using Microsoft.VisualStudio.Text.Operations)
  (clr-using IronScheme.VisualStudio)

  (define (open-views)
    (clr-call Shell OpenViews '()))

  (define view (make-parameter '()))

  (define (make-uppercase)
    (clr-call IEditorOperations MakeUppercase (view)))
  
)
