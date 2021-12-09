; License
; Copyright (c) 2016-2021 Llewellyn Pritchard
; All rights reserved.
; This source code is subject to terms and conditions of the BSD License.

(library (editor)
  (export
    editor
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

  (define editor (make-parameter '()))

  (define (make-uppercase)
    (clr-call IEditorOperations MakeUppercase (editor)))
  
)
